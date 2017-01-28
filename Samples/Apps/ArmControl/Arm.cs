using System;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;

namespace ArmControl
{
    public class Arm
    {
        private readonly Servo grip, hand, wrist, elbow, shoulder, mount;

        public Arm(IMicrocontroller mcu)
        {
            grip     = new Servo(mcu,  7,  700, 2000);
            hand     = new Servo(mcu,  6,  500, 2500);
            wrist    = new Servo(mcu,  8,  500, 2400);
            elbow    = new Servo(mcu,  5,  700, 2100);
            shoulder = new Servo(mcu,  9,  800, 2300);
            mount    = new Servo(mcu, 10, 1000, 2000);
        }

        private async Task InitServo(Servo servo)
        {
            await servo.Initialize();
            await servo.Write(0);
        }

        public async Task Initialize()
        {
            await InitServo(grip);
            await InitServo(hand);
            await InitServo(wrist);
            await InitServo(elbow);
            await InitServo(shoulder);
            await InitServo(mount);
        }

        public async Task Grip(float value)
        {
            await grip.Write(value);
        }

        public async Task Hand(float value)
        {
            await hand.Write(value);
        }

        public async Task Wrist(float value)
        {
            await wrist.Write(value);
        }

        public async Task Elbow(float value)
        {
            await elbow.Write(value);
        }

        public async Task Shoulder(float value)
        {
            await shoulder.Write(value);
        }

        public async Task Mount(float value)
        {
            await mount.Write(value);
        }
    }
}
