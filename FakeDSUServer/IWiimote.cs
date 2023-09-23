using System;
using System.Net.NetworkInformation;
using System.Numerics;

namespace FakeDSUServer
{
    public interface IWiimote
    {
        public byte Id { get; set; }
        public byte LED => (byte)(1 << Id);
        public BatteryState Battery { get; set; }
        public ConnectState ConnectionState { get; set; }
        public DeviceModel Model { get; set; }
        public ConnectionType Connection { get; set; }
        public string? SerialNumber { get; set; }
        public PhysicalAddress MacAddress { get; set; }

        public Buttons1 ButtonSet1 { get; set; }
        public Buttons2 ButtonSet2 { get; set; }
        public bool HomeButtonPressed { get; set; }


        public Vector3 Gyro { get; set; }
        public Vector3 Accelerometer { get; set; }

        public uint GetPacketNumber();

        public enum ConnectionType : byte
        {
            NotApplicable = 0,
            USB = 1,
            Bluetooth = 2,
        }

        public enum ConnectState : byte
        {
            NotConnected = 0,
            Reserved = 1,
            Connected = 2,
        }

        public enum DeviceModel : byte
        {
            NotApplicable = 0,
            NoGyro = 1,
            FullGyro = 2,
            DontUse = 3,
        }

        public enum BatteryState : byte
        {
            NotApplicable = 0,
            Dying = 1, 
            Low = 2,
            Medium = 3, 
            High = 4,
            Full = 5,
            Charging = 0xEE,
            Charged = 0xEF,
        }

        [Flags]
        public enum Buttons1 : byte
        {
            DPadLeft = 1 << 7,
            DPadDown = 1 << 6,
            DPadRight = 1 << 5,
            DPadUp = 1 << 4,
            Unused1 = 1 << 3, // Options
            Unused2 = 1 << 2, // R3
            Unused3 = 1 << 1, // L3
            Unused4 = 1, // Share
        }
        [Flags]
        public enum Buttons2 : byte
        {
            One = 1 << 7, // Y
            B = 1 << 6, // B
            A = 1 << 5, // A
            Two = 1 << 4, // X
            Plus = 1 << 3, // R1
            Minus = 1 << 2, // L1
            Unused5 = 1 << 1, // R2
            Unused6 = 1 << 0, // L2
        }
    }
}
