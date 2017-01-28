using System;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SampleOmniDriveApp
{
    class Program
    {
        const string PORT = "com12";

        static IMicrocontroller mcu;

        static async Task Disconnect()
        {
            await mcu.Disconnect();
        }

        static async Task ControlOmni()
        {
            Console.WriteLine("Connecting to MCU ({0})...", PORT);
            mcu = new Microcontroller(new SerialTransport(PORT));
            await mcu.Connect();

            Console.WriteLine("Attaching omni drive...");
            var omni = new OmniDrive(mcu);
            await omni.Initialize();

            
            Console.WriteLine("Control with left thumbstick. Press 'Back' to disconnect.");

            var lastY = float.NaN;
            var lastX = float.NaN;
            var lastShoulderLeft = ButtonState.Released;
            var lastShoulderRight = ButtonState.Released;

            GamePadState pad;

            do
            {
                pad = GamePad.GetState(PlayerIndex.One);

                var y = pad.ThumbSticks.Left.Y;
                var x = pad.ThumbSticks.Left.X;
                var shoulderLeft = pad.Buttons.LeftShoulder;
                var shoulderRight = pad.Buttons.RightShoulder;

                if (x.CompareTo(lastX) != 0)
                {
                    lastX = x;
                    Console.WriteLine("X: {0}", x);
                    if (x < 0)
                        await omni.DriveLeft((int)Math.Abs(x * 100));
                    else if (x > 0)
                        await omni.DriveRight((int)(Math.Abs(x * 100)));
                    else
                        await omni.AllStop();
                }
                if (y.CompareTo(lastY) != 0)
                {
                    lastY = y;
                    Console.WriteLine("Y: {0}", y);
                    if (y < 0)
                        await omni.DriveForward((int)Math.Abs(y * 100));
                    else if (y > 0)
                        await omni.DriveBackward((int)(Math.Abs(y * 100)));
                    else
                        await omni.AllStop();
                }

                if (shoulderLeft.CompareTo(lastShoulderLeft) != 0)
                {
                    lastShoulderLeft = shoulderLeft;
                    Console.WriteLine("ShoulderLeft : {0}", shoulderLeft);
                    if (shoulderLeft == ButtonState.Pressed)
                    {
                        await omni.RotateLeft(100);
                    }
                    else
                    {
                        await omni.AllStop();
                    }
                }
                if (shoulderRight.CompareTo(lastShoulderRight) != 0)
                {
                    lastShoulderRight = shoulderRight;
                    Console.WriteLine("ShoulderRight : {0}", shoulderRight);
                    if (shoulderRight == ButtonState.Pressed)
                    {
                        await omni.RotateRight(100);
                    }
                    else
                    {
                        await omni.AllStop();
                    }

                }

            } while (pad.Buttons.Back != ButtonState.Pressed);

            Console.WriteLine("Disconnecting from MCU...");
            await mcu.Disconnect();
        }

        static void Main(string[] args)
        {
            ControlOmni();
            Console.ReadLine();
        }
    }
}