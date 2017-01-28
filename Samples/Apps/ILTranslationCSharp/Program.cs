using System;
using System.Threading.Tasks;
using Microsoft.Robotics.Brief;
using Microsoft.Robotics.Microcontroller;
using MCU = Microsoft.Robotics.Microcontroller.Microcontroller;

namespace ILTranslationCSharp
{
    public class Blinker
    {
        private readonly int pin;
        private readonly IMicrocontroller mcu;

        public Blinker(int pin, IMicrocontroller mcu)
        {
            this.pin = pin;
            this.mcu = mcu;

            mcu.Instruction("delay", 100);
            mcu.BindAction("delay", Delay);

            mcu.DefineAction(Blink);
            mcu.DefineAction(S);
            mcu.DefineAction(O);
        }

        public static void Delay(int milliseconds)
        {
            throw new NotImplementedException();
        }

        private static void Init(int pin)
        {
            MCU.PinMode(MCU.Output, pin);
        }

        public async Task InitAsync()
        {
            MCU.Initialize(mcu);
            await mcu.ExecuteAction(Init, pin);
        }

        private static void Blink(int pin, int delay)
        {
            MCU.DigitalWrite(MCU.High, pin);
            Delay(delay);
            MCU.DigitalWrite(MCU.Low, pin);
            Delay(delay);
        }

        private const int dit = 100;

        private static void S(int pin)
        {
            for (var i = 0; i < 3; i++)
                Blink(pin, dit);
            Delay(dit);
        }

        private const int da = 300;

        private static void O(int pin)
        {
            for (var i = 0; i < 3; i++)
                Blink(pin, da);
            Delay(dit);
        }

        private static void SOS(int pin)
        {
            S(pin);
            O(pin);
            S(pin);
        }

        public async Task Message()
        {
            await mcu.ExecuteAction(SOS, pin);
        }
    }

    class Program
    {
        const string PORT = "com16";

        static IMicrocontroller mcu;

        static async Task Disconnect()
        {
            await mcu.Disconnect();
        }

        static async Task Demo()
        {
            Console.WriteLine("Connecting to MCU ({0})...", PORT);
            mcu = new Microcontroller(new SerialTransport(PORT));
            await mcu.Connect();

            Console.WriteLine("Blinking SOS...");
            const int LED_PIN = 9;
            var blinker = new Blinker(LED_PIN, mcu);
            await blinker.InitAsync();
            await blinker.Message();

            Console.WriteLine("Disconnecting from MCU...");
            await mcu.Disconnect();
        }

        static void Main(string[] args)
        {
            Demo();
            Console.ReadLine();
        }
    }
}
