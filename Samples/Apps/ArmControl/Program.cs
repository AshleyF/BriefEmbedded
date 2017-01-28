using System;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ArmControl
{
    class Program
    {
        const string PORT = "com16";

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

            Console.WriteLine("Attaching arm...");
            var arm = new Arm(mcu);
            await arm.Initialize();

            Console.WriteLine("Press 'Back' to disconnect.");

            var lastLeftTrigger = float.NaN;
            var lastRightTrigger = float.NaN;
            var lastLeftX = float.NaN;
            var lastLeftY = float.NaN;
            var lastRightX = float.NaN;
            var lastRightY = float.NaN;

            GamePadState pad;

            do
            {
                pad = GamePad.GetState(PlayerIndex.One);

                var leftTrigger = pad.Triggers.Left;
                var rightTrigger = pad.Triggers.Right;
                var leftX = pad.ThumbSticks.Left.X;
                var leftY = pad.ThumbSticks.Left.Y;
                var rightX = pad.ThumbSticks.Right.X;
                var rightY = pad.ThumbSticks.Right.Y;

                // grip
                if (rightTrigger.CompareTo(lastRightTrigger) != 0)
                {
                    lastRightTrigger = rightTrigger;
                    Console.WriteLine("Grip: {0}", rightTrigger);
                    await arm.Grip(rightTrigger * 2 - 1);
                }

                // hand
                if (leftX.CompareTo(lastLeftX) != 0)
                {
                    lastLeftX = leftX;
                    Console.WriteLine("Hand: {0}", leftX);
                    await arm.Hand(-leftX);
                }

                // wrist
                if (leftY.CompareTo(lastLeftY) != 0)
                {
                    lastLeftY = leftY;
                    Console.WriteLine("Wrist: {0}", leftY);
                    await arm.Wrist(-leftY);
                }

                // elbow
                if (rightY.CompareTo(lastRightY) != 0)
                {
                    lastRightY = rightY;
                    Console.WriteLine("Elbow: {0}", rightY);
                    await arm.Elbow(rightY);
                }

                // shoulder
                if (rightX.CompareTo(lastRightX) != 0)
                {
                    lastRightX = rightX;
                    Console.WriteLine("Shoulder: {0}", rightX);
                    await arm.Shoulder(rightX);
                }

                // mount
                if (leftTrigger.CompareTo(lastLeftTrigger) != 0)
                {
                    lastLeftTrigger = leftTrigger;
                    Console.WriteLine("Mount: {0}", leftTrigger);
                    await arm.Mount(leftTrigger * 2 - 1);
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
