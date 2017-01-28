using System;
using System.IO.Ports;

namespace RawComm
{
    class Program
    {
        static void Main(string[] args)
        {
            // 'com16 conn
            var port = new SerialPort("com16");
            port.Open();

            // --------------------------------------------------------------------------------
            // DIRECT COMMANDS

            // reset
            port.Write(new byte[] {
                0,  // SEQ number

                56, // reset

                0,  // execute

                56, // CRC
                192 // END
                }, 0, 5);

            // output 9 pinMode
            port.Write(new byte[] {
                0,    // SEQ number

                1, 1, // literal 1  (output)
                1, 9, // literal 9
                57,   // pinMode

                0,    // execute

                49,   // CRC
                192   // END
                }, 0, 9);

            // high 9 digitalWrite
            port.Write(new byte[] {
                1,      // SEQ number

                1, 255, // literal -1  (high)
                1, 9,   // literal 9
                59,     // digitalWrite

                0,      // execute

                204,    // CRC
                192     // END
                }, 0, 9);

            // low 9 digitalWrite
            port.Write(new byte[] {
                2,    // SEQ number

                1, 0, // literal 0  (low)
                1, 9, // literal 9
                59,   // digitalWrite

                0,    // execute

                48,   // CRC
                192   // END
                }, 0, 9);

            // --------------------------------------------------------------------------------
            // DEFINITIONS

            // [high 9 digitalWrite] 'on def
            port.Write(new byte[] {
                3,      // SEQ number

                1, 255, // literal -1  (high)
                1, 9,   // literal 9
                59,     // digitalWrite
                0,      // return  (terminate definition)

                1,      // define

                207,    // CRC
                192     // END
                }, 0, 10);

            // on
            port.Write(new byte[] {
                4,      // SEQ number

                128, 0, // on (address 0 - high bit set)

                0,      // execute

                132,    // CRC
                192     // END
                }, 0, 6);


            // [low 9 digitalWrite] 'off def
            port.Write(new byte[] {
                5,    // SEQ number

                1, 0, // literal 0  (low)
                1, 9, // literal 9
                59,   // digitalWrite
                0,    // return  (terminate definition)

                1,    // define

                54,   // CRC
                192   // END
                }, 0, 10);

            // off
            port.Write(new byte[] {
                6,      // SEQ number

                128, 6, // off  (address 6 - high bit set)

                0,      // execute

                128,    // CRC
                192     // END
                }, 0, 6);

            // on
            port.Write(new byte[] {
                7,      // SEQ number

                128, 0, // on  (address 0 - high bit set)

                0,      // execute

                135,    // CRC
                192     // END
                }, 0, 6);

            // off
            port.Write(new byte[] {
                8,      // SEQ number

                128, 6, // off  (address 6 - high bit set)

                0,      // execute

                142,    // CRC
                192     // END
                }, 0, 6);

            // --------------------------------------------------------------------------------
            // CONCATENATION

            // [500 delay] 'pause def
            port.Write(new byte[] {
                9,      // SEQ number

                2,      // literal  (16-bit)
                1, 244, // 500      (big endian)
                100,    // delay    (custom instruction)
                0,      // return   (terminate definition)

                1,      // define

                155,    // CRC
                192     // END
                }, 0, 9);

            // on pause off pause on pause off
            port.Write(new byte[] {
                10,   // SEQ number

                128, 0,  // on     (address 0  - high bit set)
                128, 12, // pause  (address 12 - high bit set)
                128, 6,  // off    (address 6  - high bit set)
                128, 12, // pause  (address 12 - high bit set)
                128, 0,  // on     (address 0  - high bit set)
                128, 12, // pause  (address 12 - high bit set)
                128, 6,  // off    (address 6  - high bit set)

                0,   // execute

                134, // CRC
                192  // END
                }, 0, 18);

            // --------------------------------------------------------------------------------
            // EVENTS

            // 3 4 + 6 *
            port.Write(new byte[] {
                11,   // SEQ number

                1, 3, // literal 3
                1, 4, // literal 4
                15,   // +  (add)
                1, 6, // literal 6
                17,   // *  (multiply)

                0,    // execute

                21,   // CRC
                192   // END
                }, 0, 12);

            // 7 event
            port.Write(new byte[] {
                12,   // SEQ number

                1, 7, // literal 7
                10,   // event

                0,    // execute

                0,    // CRC
                192   // END
                }, 0, 7);

            // reading event data
            port.ReadByte(); // SEQ number

            var id   = port.ReadByte();
            var data = port.ReadByte();

            port.ReadByte(); // CRC
            port.ReadByte(); // END
        }
    }
}
