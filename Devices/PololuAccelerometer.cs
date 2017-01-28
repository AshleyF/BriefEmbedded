using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;

namespace Pololu.IMU
{
    // Pololu MinIMU-9 Accelerometer
    // http://www.pololu.com/catalog/product/1265

    public class Accelerometer
    {
        private readonly IMicrocontroller mcu;

        public Accelerometer(IMicrocontroller mcu)
        {
            this.mcu = mcu;
        }

        public async Task Initialize()
        {
            const int ACCEL_ADDRESS = 0x30 >> 1;
            const int ACCEL_CTRL_REG1_A = 0x20;
            const int ACCEL_OUT_Y_L_A  = 0x2A | (1 << 7);

            // normal power mode, only y-axis
            await mcu.ExecuteBrief(
                "wireBegin {0} wireBeginTransmission {1} wireWrite 34 wireWrite wireEndTransmission",
                ACCEL_ADDRESS, ACCEL_CTRL_REG1_A);

            mcu.DefineBrief("bytesToSigned16",
                "8 lsh or " + // 2 bytes
                "dup 32768 and 0 <> " + // high bit set?
                "[65535 xor 1+ neg] if"); // manual 16-bit two's complement

            mcu.DefineBrief("acceleration",
                // assert MSB of address; get accelerometer to do slave-transmit subaddress updating
                "{0} wireBeginTransmission {1} wireWrite wireEndTransmission " +
                "2 {0} wireRequestFrom wireRead wireRead bytesToSigned16", // read y-axis
                ACCEL_ADDRESS, ACCEL_OUT_Y_L_A);

            var eventId = mcu.AllocateEventId(data =>
            {
                awaitingEvent.Dequeue()(Utility.BytesAsInt(data));
            });
            mcu.DefineBrief("pollAcceleration", "acceleration {0} event", eventId);
        }

        private Queue<Action<int>> awaitingEvent = new Queue<Action<int>>();

        public Task<int> Acceleration()
        {
            var tcs = new TaskCompletionSource<int>();
            Task.Run(() =>
                {
                    lock (awaitingEvent)
                    {
                        awaitingEvent.Enqueue(tcs.SetResult);
                    }
                    mcu.ExecuteBrief("pollAcceleration");
                });
            return tcs.Task;
        }
    }
}