using System;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ServoControl
{
    class Program
    {
        const string PORT = "com4";

        static IMicrocontroller mcu;

        static async Task Disconnect()
        {
            await mcu.Disconnect();
        }

        static async Task ControlServos()
        {
            Console.WriteLine("Connecting to MCU ({0})...", PORT);
            mcu = new Microcontroller(new SerialTransport(PORT));
            await mcu.Connect();

            Console.WriteLine("Attaching servos...");
            var pan = new Servo(mcu, 1, 1000, 2000);
            await pan.Initialize();
            var tilt = new Servo(mcu, 0, 1000, 2000);
            await tilt.Initialize();

            Console.WriteLine("Control servos with left thumbstick. Press 'Back' to disconnect.");

            var lastY = float.NaN;
            var lastX = float.NaN;

            GamePadState pad;

            do
            {
                pad = GamePad.GetState(PlayerIndex.One);

                var y = pad.ThumbSticks.Left.Y;
                var x = pad.ThumbSticks.Left.X;

                if (x.CompareTo(lastX) != 0)
                {
                    lastX = x;
                    Console.WriteLine("X: {0}", x);
                    await pan.Write(x);
                }
                if (y.CompareTo(lastY) != 0)
                {
                    lastY = y;
                    Console.WriteLine("Y: {0}", y);
                    await tilt.Write(y);
                }
            } while (pad.Buttons.Back != ButtonState.Pressed);

            Console.WriteLine("Disconnecting from MCU...");
            await mcu.Disconnect();
        }

        static void Main(string[] args)
        {
            ControlServos();
            Console.ReadLine();
        }
    }
}
