using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;

namespace Pololu.IMU
{
    // Pololu MinIMU-9 Gyro
    // http://www.pololu.com/catalog/product/1265

    public class Gyro
    {
        private readonly IMicrocontroller mcu;

        public Gyro(IMicrocontroller mcu)
        {
            this.mcu = mcu;
        }

        public async Task Initialize()
        {
            const int GYRO_ADDRESS = 0xD2 >> 1;
            const int GYRO_CTRL_REG1 = 0x20;
            const int GYRO_CTRL_REG4 = 0x23;
            const int GYRO_OUT_X_L = 0x28 | (1 << 7);

            mcu.DefineBrief("wireWriteRegister", "wireBeginTransmission wireWrite wireWrite wireEndTransmission");

            await mcu.ExecuteBrief(
                "wireBegin " +
                "9 {1} {0} writeRegister" + // normal power mode, only x-axis
                "32 {2} {0} writeRegister", // 2000 dps full scale (70 mdps sensitivity)
                GYRO_ADDRESS, GYRO_CTRL_REG1, GYRO_CTRL_REG4);

            mcu.DefineBrief("bytesToSigned16",
                "8 lsh or " + // 2 bytes
                "dup 32768 and 0 <> " + // high bit set?
                "[65535 xor 1+ neg] if"); // manual 16-bit two's complement

            mcu.DefineBrief("gyroRaw",
                // assert MSB of address; get gyro to do slave-transmit subaddress updating
                "{0} wireBeginTransmission {1} wireWrite wireEndTransmission " +
                "{0} 2 wireRequestFrom wireRead wireRead bytesToSigned16" // read x-axis
                , GYRO_ADDRESS, GYRO_OUT_X_L);

            const int GAIN = 7; // 100ths (70 mdps sensitivity)
            const int NOISE_THRESHOLD = 8; // 0.56 degrees

            mcu.DefineBrief("gyroDps", "gyroRaw dup abs {0} < [drop 0] if {1} *", NOISE_THRESHOLD, GAIN);

            var rawEventId = mcu.AllocateEventId(data =>
            {
                awaitingRawEvent.Dequeue()(Utility.BytesAsInt(data));
            });
            mcu.DefineBrief("pollRaw", "gyroRaw {0} event", rawEventId);

            var dpsEventId = mcu.AllocateEventId(data =>
            {
                awaitingDpsEvent.Dequeue()(Utility.BytesAsInt(data));
            });
            mcu.DefineBrief("pollDps", "gyroDps {0} event", dpsEventId);
        }

        private Queue<Action<int>> awaitingRawEvent = new Queue<Action<int>>();

        public Task<int> AngularSpeedRaw()
        {
            var tcs = new TaskCompletionSource<int>();
            Task.Run(() =>
                {
                    lock (awaitingRawEvent)
                    {
                        awaitingRawEvent.Enqueue(tcs.SetResult);
                    }
                    mcu.ExecuteBrief("pollRaw");
                });
            return tcs.Task;
        }

        private Queue<Action<int>> awaitingDpsEvent = new Queue<Action<int>>();

        public Task<float> AngularSpeedDps()
        {
            var tcs = new TaskCompletionSource<float>();
            Task.Run(() =>
                {
                    lock (awaitingDpsEvent)
                    {
                        awaitingDpsEvent.Enqueue(dps => tcs.SetResult(dps / 100));
                    }
                    mcu.ExecuteBrief("pollDps");
                });
            return tcs.Task;
        }
    }
}