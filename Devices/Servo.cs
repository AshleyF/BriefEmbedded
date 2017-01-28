#define BRIEF_VERSION

using System;
using System.Threading.Tasks;
using Microsoft.Robotics.Brief;
using Microsoft.Robotics.Microcontroller;
using MCU = Microsoft.Robotics.Microcontroller.Microcontroller;

namespace Microsoft.Robotics
{
#if IL_VERSION

    public class Servo
    {
        private readonly IMicrocontroller mcu;
        private readonly int pin;
        private readonly int minMilliseconds;
        private readonly int maxMilliseconds;

        public Servo(IMicrocontroller mcu, int pin, int minMilliseconds, int maxMilliseconds)
        {
            this.mcu = mcu;
            this.pin = pin;
            this.minMilliseconds = minMilliseconds;
            this.maxMilliseconds = maxMilliseconds;
            MCU.Initialize(mcu);
        }

        public async Task Initialize()
        {
            await mcu.ExecuteAction(MCU.ServoAttach, pin);
        }

        public async Task Write(float value)
        {
            var ms = (int)((value + 1) * (maxMilliseconds - minMilliseconds) / 2 + minMilliseconds);
            await mcu.ExecuteAction(MCU.ServoWriteMicros, ms, pin);
        }
    }

#endif
#if BRIEF_VERSION

    public class Servo
    {
        private readonly IMicrocontroller mcu;
        private readonly int pin;
        private readonly int minMilliseconds;
        private readonly int maxMilliseconds;

        public Servo(IMicrocontroller mcu, int pin, int minMilliseconds, int maxMilliseconds)
        {
            this.mcu = mcu;
            this.pin = pin;
            this.minMilliseconds = minMilliseconds;
            this.maxMilliseconds = maxMilliseconds;
        }

        public async Task Initialize()
        {
            await mcu.ExecuteBrief("servoAttach", pin);
        }

        public async Task Write(float value)
        {
            var ms = (int)((value + 1) * (maxMilliseconds - minMilliseconds) / 2 + minMilliseconds);
            await mcu.ExecuteBrief("servoWriteMicros", ms, pin);
        }
    }
#endif
}