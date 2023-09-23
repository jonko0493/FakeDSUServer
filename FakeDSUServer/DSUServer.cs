using Force.Crc32;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace FakeDSUServer
{
    public class DSUServer
    {
        //private Timer? _wiimotePollTimer;
        private readonly ILogger _logger;
        private Socket? _udpSocket;
        private uint _serverId;
        private byte[] _receiveBuffer = new byte[1024];
        public bool Running { get; private set; }

        public List<IWiimote> Wiimotes { get; set; } = new();

        public const ushort ProtocolVersion = 1001;

        public DSUServer(ILogger<DSUServer> logger)
        {
            _logger = logger;
        }
        public DSUServer()
        {
            _logger = LoggerFactory.Create(log => log.AddConsole()).CreateLogger<DSUServer>();
        }

        public enum RequestType : uint
        {
            DSUC_Version = 0x100000,
            DSUC_ListPorts = 0x100001,
            DSUC_PadData = 0x100002,
        }
        
        public enum ResponseType : uint
        {
            DSUS_Version = 0x100000,
            DSUS_PortInfo = 0x100001,
            DSUS_PadData = 0x100002,
        }

        public void ConnectWiimote(IWiimote wiimote)
        {
            Wiimotes.Add(wiimote);
            //if (_wiimotePollTimer is null)
            //{
            //    _wiimotePollTimer = new(5);
            //    _wiimotePollTimer.Elapsed += PollWiimotes;
            //    if (Running)
            //    {
            //        _wiimotePollTimer.Enabled = true;
            //        _wiimotePollTimer.Start();
            //    }
            //}
        }

        public void Start(IPAddress address, int port = 26760)
        {
            if (Running)
            {
                if (_udpSocket is not null)
                {
                    _udpSocket.Close();
                    _udpSocket = null;
                }
                //if (_wiimotePollTimer is not null)
                //{
                //    _wiimotePollTimer.Stop();
                //    _wiimotePollTimer.Enabled = false;
                //}
                Running = false;
            }

            _udpSocket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                _logger.LogInformation($"Attempting to bind to socket with address {address} on port {port}...");
                _udpSocket.Bind(new IPEndPoint(address, port));
            }
            catch (SocketException e)
            {
                _udpSocket.Close();
                _udpSocket = null;

                _logger.LogError($"Faied to bind to socket with address {address} on port {port}: {e.Message}\n\n{e.StackTrace}");
            }

            _serverId = (uint)new Random().Next(int.MinValue, int.MaxValue);

            Running = true;
            //if (_wiimotePollTimer is not null)
            //{
            //    _wiimotePollTimer.Enabled = true;
            //    _wiimotePollTimer.Start();
            //}
            _logger.LogInformation("Successfully started server!");

            StartReceive();
        }

        public void Stop()
        {
            Running = false;
            if (_udpSocket is not null)
            {
                _udpSocket.Close();
                _udpSocket = null;
            }
            //if (_wiimotePollTimer is not null)
            //{
            //    _wiimotePollTimer.Enabled = false;
            //    _wiimotePollTimer.Stop();
            //}
        }

        private void StartReceive()
        {
            try
            {
                if (Running && _udpSocket is not null)
                {
                    EndPoint newClientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                    _udpSocket.BeginReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref newClientEndpoint, ReceiveCallback, _udpSocket);
                }
            }
            catch (SocketException)
            {
                uint sioUdpConnreset = 0x80000000 | 0x18000000 | 12;
                _udpSocket?.IOControl((int)sioUdpConnreset, new byte[] { 0 }, null);

                StartReceive();
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            byte[]? localMessage = null;
            EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                if (result.AsyncState is null)
                {
                    throw new Exception("AsyncState was null!");
                }

                Socket receivingSocket = (Socket)result.AsyncState;
                int messageLength = receivingSocket.EndReceiveFrom(result, ref clientEndPoint);
                localMessage = new byte[messageLength];
                Array.Copy(_receiveBuffer, localMessage, messageLength);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred: {ex.Message}\n\n{ex.StackTrace}");
            }

            StartReceive();

            if (localMessage is not null)
            {
                ProcessIncoming(localMessage, (IPEndPoint)clientEndPoint);
            }
        }

        private void ProcessIncoming(byte[] localMessage, IPEndPoint clientEndPoint)
        {
            try
            {
                int currentIndex = 0;

                if (Encoding.ASCII.GetString(localMessage.Take(4).ToArray()) != "DSUC")
                {
                    _logger.LogInformation("Incoming message did not have standard DSU client header, ignoring...");
                    return;
                }
                currentIndex += 4;

                uint protocolVersion = BitConverter.ToUInt16(localMessage, currentIndex);
                currentIndex += 2;

                if (protocolVersion != ProtocolVersion)
                {
                    _logger.LogInformation($"Incoming message had protocol version {protocolVersion} which did not match expected version {ProtocolVersion}, ignoring...");
                    return;
                }

                uint packetSize = BitConverter.ToUInt16(localMessage, currentIndex);
                currentIndex += 2;

                if (packetSize < 0)
                {
                    _logger.LogInformation($"Invalid packet size of {packetSize}, ignoring...");
                    return;
                }

                packetSize += 16; // add header size
                if (packetSize > localMessage.Length)
                {
                    _logger.LogInformation($"Actual packet size {localMessage.Length} was greater than specified size {packetSize}, ignoring...");
                    return;
                }
                else if (packetSize < localMessage.Length)
                {
                    byte[] newMessage = new byte[packetSize];
                    Array.Copy(localMessage, newMessage, packetSize);
                    localMessage = newMessage;
                }

                uint crcValue = BitConverter.ToUInt32(localMessage, currentIndex);
                // zero out the CRC in the packet once we have it since that's how it was calculated
                localMessage[currentIndex++] = 0;
                localMessage[currentIndex++] = 0;
                localMessage[currentIndex++] = 0;
                localMessage[currentIndex++] = 0;

                uint calculatedCrc = Crc32Algorithm.Compute(localMessage);
                if (crcValue != calculatedCrc)
                {
                    _logger.LogInformation($"Incoming packet CRC {crcValue} does not match calculated CRC {calculatedCrc}");
                    return;
                }

                uint clientId = BitConverter.ToUInt32(localMessage, currentIndex);
                currentIndex += 4;

                RequestType messageType = (RequestType)BitConverter.ToUInt32(localMessage, currentIndex);
                currentIndex += 4;

                switch (messageType)
                {
                    case RequestType.DSUC_Version:
                        List<byte> outputVersionData = new();
                        outputVersionData.AddRange(BitConverter.GetBytes((uint)ResponseType.DSUS_Version));
                        outputVersionData.AddRange(BitConverter.GetBytes(ProtocolVersion));
                        outputVersionData.AddRange(new byte[2]);

                        SendPacket(clientEndPoint, outputVersionData.ToArray());
                        break;

                    case RequestType.DSUC_ListPorts:
                        int numPadRequests = BitConverter.ToInt32(localMessage, currentIndex);
                        currentIndex += 4;
                        if (numPadRequests < 0 || numPadRequests > 4)
                        {
                            _logger.LogInformation($"Information requested for an invalid number of pads ({numPadRequests})");
                            return;
                        }

                        for (byte i = 0; i < numPadRequests && i < Wiimotes.Count; i++)
                        {
                            List<byte> outputPortsData = new();
                            AddInitialOutputPacketData(outputPortsData, Wiimotes[i]);
                            outputPortsData.Add(0);

                            SendPacket(clientEndPoint, outputPortsData.ToArray());
                            _logger.LogInformation($"Sent packet for wiimote {Wiimotes[i].Id}");
                        }
                        break;

                    case RequestType.DSUC_PadData:
                        byte regFlags = localMessage[currentIndex++];
                        byte idToReg = localMessage[currentIndex++];
                        PhysicalAddress macToReg = new(localMessage.Skip(currentIndex).Take(6).ToArray());
                        currentIndex += 6;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while processing incoming message: {ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private void PollWiimotes(object? sender, ElapsedEventArgs e)
        {
            foreach (IWiimote wiimote in Wiimotes)
            {
                List<byte> packetData = new();
                AddInitialOutputPacketData(packetData, wiimote);
                packetData.Add(1);
                packetData.AddRange(BitConverter.GetBytes(wiimote.GetPacketNumber()));

                packetData.Add((byte)wiimote.ButtonSet1);
                packetData.Add((byte)wiimote.ButtonSet2);
                packetData.Add((byte)(wiimote.HomeButtonPressed ? 1 : 0));
                packetData.Add(0);
                packetData.AddRange(new byte[] { 128, 128, 128, 128 }); // sticks
                packetData.Add((byte)(wiimote.ButtonSet1.HasFlag(IWiimote.Buttons1.DPadLeft) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet1.HasFlag(IWiimote.Buttons1.DPadDown) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet1.HasFlag(IWiimote.Buttons1.DPadRight) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet1.HasFlag(IWiimote.Buttons1.DPadUp) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.One) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.B) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.A) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.Two) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.Plus) ? 255 : 0));
                packetData.Add((byte)(wiimote.ButtonSet2.HasFlag(IWiimote.Buttons2.Minus) ? 255 : 0));
                packetData.AddRange(new byte[2]); // R2/L2
                packetData.AddRange(new byte[12]); // touch pads
                packetData.AddRange(BitConverter.GetBytes(DateTimeOffset.Now.Ticks));
                packetData.AddRange(BitConverter.GetBytes(wiimote.Accelerometer.Y));
                packetData.AddRange(BitConverter.GetBytes(-wiimote.Accelerometer.Z));
                packetData.AddRange(BitConverter.GetBytes(wiimote.Accelerometer.X));
                packetData.AddRange(BitConverter.GetBytes(wiimote.Gyro.Y));
                packetData.AddRange(BitConverter.GetBytes(wiimote.Gyro.Z));
                packetData.AddRange(BitConverter.GetBytes(wiimote.Gyro.X));
                _logger.LogInformation($"Sent state data for wiimote {wiimote.Id}");
            }
        }

        private static void AddInitialOutputPacketData(List<byte> packetData, IWiimote wiimote)
        {
            packetData.Add(wiimote.Id);
            packetData.Add((byte)wiimote.ConnectionState);
            packetData.Add((byte)wiimote.Model);
            packetData.Add((byte)wiimote.Connection);
            packetData.AddRange(wiimote.MacAddress.GetAddressBytes());
            packetData.Add((byte)wiimote.Battery);
        }

        private void SendPacket(IPEndPoint clientEndPoint, byte[] usefulData)
        {
            List<byte> packetData = new();

            packetData.AddRange(Encoding.ASCII.GetBytes("DSUS"));
            packetData.AddRange(BitConverter.GetBytes(ProtocolVersion));
            packetData.AddRange(BitConverter.GetBytes((ushort)usefulData.Length));
            packetData.AddRange(new byte[4]); // CRC
            packetData.AddRange(BitConverter.GetBytes(_serverId));

            packetData.AddRange(usefulData);

            uint crc = Crc32Algorithm.Compute(packetData.ToArray());
            packetData.RemoveRange(8, 4);
            packetData.InsertRange(8, BitConverter.GetBytes(crc));

            try
            {
                _udpSocket?.SendTo(packetData.ToArray(), clientEndPoint);
                _logger.LogInformation($"Packet sent: {string.Join(' ', packetData.Select(b => $"{b:X2}"))}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred while attempting to send packet to client: {ex.Message}\n\n{ex.StackTrace}");
            }
        }
    }
}