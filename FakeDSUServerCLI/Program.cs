using FakeDSUServer;
using System;

namespace FakeDSUServerCLI
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CliWiimote wiimote = new();
            DSUServer server = new();
            server.ConnectWiimote(wiimote);
            server.Start(new(new byte[] { 127, 0, 0, 1 }));
            bool loop = true;
            while (loop)
            {
                ConsoleKeyInfo key = Console.ReadKey();
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        loop = false;
                        break;

                    case ConsoleKey.A:
                        wiimote.ToggleA();
                        break;

                    case ConsoleKey.B:
                        wiimote.ToggleB();
                        break;
                }
            }
        }
    }
}