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

                    case ConsoleKey.UpArrow:
                        wiimote.ToggleDPadUp();
                        break;

                    case ConsoleKey.DownArrow:
                        wiimote.ToggleDPadDown();
                        break;

                    case ConsoleKey.LeftArrow:
                        wiimote.ToggleDPadLeft();
                        break;

                    case ConsoleKey.RightArrow:
                        wiimote.ToggleDPadRight();
                        break;

                    case ConsoleKey.D1:
                        wiimote.Toggle1();
                        break;

                    case ConsoleKey.D2:
                        wiimote.Toggle2();
                        break;

                    case ConsoleKey.A:
                        wiimote.ToggleA();
                        break;

                    case ConsoleKey.B:
                        wiimote.ToggleB();
                        break;

                    case ConsoleKey.Q:
                        wiimote.ToggleMinus();
                        break;

                    case ConsoleKey.W:
                        wiimote.TogglePlus();
                        break;

                    case ConsoleKey.Spacebar:
                        wiimote.ToggleHome();
                        break;
                }
            }
        }
    }
}