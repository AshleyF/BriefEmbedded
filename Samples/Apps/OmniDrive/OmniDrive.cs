using System;
using System.Threading.Tasks;
using Microsoft.Robotics.Microcontroller;
using Microsoft.Robotics.Brief;

namespace Microsoft.Robotics
{
    public class OmniDrive
    {
        private readonly IMicrocontroller mcu;

        private const int leftDirection = 11;
        private const int leftSpeed = 10;
        private const int rightDirection = 8;
        private const int rightSpeed = 9;
        private const int backDirection = 2;
        private const int backSpeed = 3;

        public OmniDrive(IMicrocontroller mcu)
        {
            this.mcu = mcu;
        }

        public async Task Initialize()
        {
            // setup pins

            mcu.Define("setup", "dup output swap pinMode low swap digitalWrite");
            
            await mcu.Execute(
                "setup setup setup setup setup setup",
                leftDirection, leftSpeed,
                rightDirection, rightSpeed,
                backDirection, backSpeed);
          
            // wheel pins

            mcu.Define("left",  "11 10");
            mcu.Define("right", "8 9");
            mcu.Define("back",  "2 3");

            // primitive commands

            mcu.Define("stop",    "0 swap analogWrite drop");
            mcu.Define("allStop", "left stop  right stop  back stop");

            mcu.Define("drive",   "3 pick swap analogWrite digitalWrite drop");

            mcu.Define("driveForward",  "dup  low  right drive  high left drive  0 low back drive");
            mcu.Define("driveBackward", "dup  high right drive  low  left drive  0 low back drive");

            mcu.Define("driveLeft",  "dup  high back drive  2 / dup  low  left drive  low  right drive");
            mcu.Define("driveRight", "dup  low  back drive  2 / dup  high left drive  high right drive");

            mcu.Define("rotateLeft",  "dup dup  low right drive  low left drive  low back drive");
            mcu.Define("rotateRight", "dup dup  high right drive  high left drive  high back drive");
        }

        public enum Wheel
        {
            Left, Right, Back
        }

        public enum Direction
        {
            Forward, Backward
        }

        private string WheelName(Wheel wheel)
        {
            switch (wheel)
            {
                case Wheel.Left:  return "left";
                case Wheel.Right: return "right";
                case Wheel.Back:  return "back";
            }
            throw new ArgumentException();
        }

        private string DirectionName(Direction direction)
        {
            switch (direction)
            {
                case Direction.Forward:  return "high";
                case Direction.Backward: return "low";
            }
            throw new ArgumentException();
        }

        public async Task AllStop()
        {
            await mcu.Execute("allStop");
        }

        private async Task Drive(int speed, string direction, string wheel)
        {
            await mcu.Execute(string.Format("{0} {1} drive", direction, wheel), speed);
        }

        public async Task Drive(int speed, Direction direction, Wheel wheel)
        {
            await Drive(speed, DirectionName(direction), WheelName(wheel));
        }

        public async Task DriveForward(int speed)
        {
            await mcu.Execute("driveForward", speed);
        }

        public async Task DriveBackward(int speed)
        {
            await mcu.Execute("driveBackward", speed);
        }

        public async Task DriveLeft(int speed)
        {
            await mcu.Execute("driveLeft", speed);
        }

        public async Task DriveRight(int speed)
        {
            await mcu.Execute("driveRight", speed);
        }

        public async Task RotateLeft(int speed)
        {
            await mcu.Execute("rotateLeft", speed);
        }

        public async Task RotateRight(int speed)
        {
            await mcu.Execute("rotateRight", speed);
        }
    }
}