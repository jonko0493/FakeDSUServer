using FakeDSUServer;
using System.Net.NetworkInformation;
using System.Numerics;

namespace FakeDSUServerCLI
{
    public class CliWiimote : IWiimote
    {
        public byte Id { get; set; }
        public IWiimote.BatteryState Battery { get; set; } = IWiimote.BatteryState.Full;
        public IWiimote.ConnectState ConnectionState { get; set; } = IWiimote.ConnectState.Connected;
        public IWiimote.DeviceModel Model { get; set; } = IWiimote.DeviceModel.FullGyro;
        public IWiimote.ConnectionType Connection { get; set; } = IWiimote.ConnectionType.Bluetooth;
        public string? SerialNumber { get; set; }
        public PhysicalAddress MacAddress { get; set; }

        public IWiimote.Buttons1 ButtonSet1 { get; set; }
        public IWiimote.Buttons2 ButtonSet2 { get; set; }
        public bool HomeButtonPressed { get; set; }

        public Vector3 Gyro { get; set; }
        public Vector3 Accelerometer { get; set; }

        public CliWiimote(byte padId = 0)
        {
            Id = 0;
            MacAddress = new(new byte[] { 1, 2, 3, 4, 5, 6 });
        }

        public uint PacketCount { get; set; }
        public uint GetPacketNumber()
        {
            if (PacketCount == uint.MaxValue)
            {
                PacketCount = 0;
            }
            return PacketCount++;
        }

        public void ToggleA()
        {
            ButtonSet2 ^= IWiimote.Buttons2.A;
        }
        public void ToggleB()
        {
            ButtonSet2 ^= IWiimote.Buttons2.B;
        }
    }
}
