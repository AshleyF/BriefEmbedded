//#define SHORT_BOT

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Robotics;
using Microsoft.Robotics.Microcontroller;
using Sparkfun.MonsterMoto;
using Pololu.IMU;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Robotics.Brief;

namespace SampleApp
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
            Microcontroller.PinMode(Microcontroller.Output, pin);
        }

        public async Task InitAsync()
        {
            Microcontroller.Initialize(mcu);
            await mcu.ExecuteAction(Init, pin);
        }

        private static void Blink(int pin, int delay)
        {
            Microcontroller.DigitalWrite(Microcontroller.High, pin);
            Delay(delay);
            Microcontroller.DigitalWrite(Microcontroller.Low, pin);
            Delay(delay);
        }

        private const int dit = 100;

        private static void S(int pin)
        {
            for (var i = 0; i < 3; i++)
                Blink(pin, dit);
        }

        private const int da = 300;

        private static void O(int pin)
        {
            for (var i = 0; i < 3; i++)
                Blink(pin, da);
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

        #region Demo 1 - Basic LEDs, etc.

        static int frames = 0;

        static void Data(object sender, DataEventArgs args)
        {
            frames++;
            //
            Console.Write("Data: {0}=", args.ID);
            switch (args.Value.Count())
            {
                case 0:
                case 1:
                case 2:
                case 4:
                    Console.Write(Utility.BytesAsInt(args.Value.ToArray()));
                    break;
                default:
                    foreach (var b in args.Value)
                        Console.Write("{0}, ", b);
                    break;
            }
            Console.WriteLine();
            //*/
        }

        static int dropped = 0;

        static void Protocol(object sender, ProtocolEventArgs args)
        {
            dropped++;
            Console.WriteLine("Protocol: {0} (fatal={1})", args.Message, args.Fatal);
        }

        static IMicrocontroller mcu;

        static async Task Connect()
        {
            mcu = new Microcontroller(new SerialTransport(PORT));
            mcu.Data += Data;
            mcu.Protocol += Protocol;
            await mcu.Connect();
        }

        static async Task Disconnect()
        {
            await mcu.Disconnect();
        }

        static void Test()
        {
            //Microcontroller.PinMode(11, 1);
        }

        static async Task Demo1()
        {
            const int LED_PIN = 9;// 11;
            //await mcu.Execute("output {0} pinMode", LED_PIN);
            //await mcu.Execute("high {0} digitalWrite", LED_PIN);
            //await mcu.Execute("low {0} digitalWrite", LED_PIN);
            //await mcu.Execute("high {0} digitalWrite", LED_PIN);
            //await mcu.Execute("low {0} digitalWrite", LED_PIN);

            var blinker = new Blinker(LED_PIN, mcu);
            await blinker.InitAsync();
            await blinker.Message();

            /*
            var gpio = new GPIO(mcu);
            gpio.PinMode(LED_PIN, 1);
            gpio.DigitalWrite(LED_PIN, -1);
            gpio.DigitalWrite(LED_PIN, 0);
            var test = gpio.DigitalRead(LED_PIN);
            */
        }

        #endregion Demo 1

        /*

        #region Demo 2 - Custom heartbeat

        static async Task Demo2()
        {
            //await mcu.DigitalWrite(0, true);
            //await mcu.DigitalWrite(0, false);

            //await mcu.PinMode(21, true);
            //var foo = await mcu.AnalogRead(21);
            //await mcu.Execute(Brief.Compile("[42 event( 21 analogRead edata )event] setLoop")); // TODO: dangerous!!
            await mcu.Execute(OldBrief.Compile("[loopTicks 10 mod 0 = [42 event( 20 analogRead edata 21 analogRead edata )event] [] choice] setLoop")); // TODO: dangerous!!
            const int wait = 5000;
            await Task.Delay(wait);
            await mcu.Execute(OldBrief.Compile("stopLoop"));
            await Task.Delay(1000); // let console catch up
            Console.WriteLine();
            Console.WriteLine("Frames/sec = {0} (dropped {1})", (float)frames / (float)wait * 1000.0, dropped);
            //await mcu.Execute(Brief.Compile("40 loopTicks event"));
            //await Task.Delay(1000); // give time for event
        }

        #endregion Demo 2

        #region Demo 3 - MonsterMoto driver

        static async Task Demo3()
        {
            var left = new Motor(mcu, 8, 7, 5, "left");
            await left.Initialize();

            var right = new Motor(mcu, 9, 4, 6, "right");
            await right.Initialize();

            await left.SetSmoothing(1);
            await left.SetTargetPwmPercentage(0.92);
            await left.SetTargetPwmPercentage(0.21);
            await left.SetTargetPwmPercentage(-0.54);
            await left.SetTargetPwmPercentage(0.0);
            await left.SetSmoothing(2);
            await left.SetTargetPwmPercentage(1);
            await left.Brake();

            await right.SetTargetPwmPercentage(-1);
            await right.Brake();

            await mcu.Execute(OldBrief.Compile("left 255 drive  right -255 drive"));
            await mcu.Execute(OldBrief.Compile("left 50 drive  right 50 drive"));
            await mcu.Execute(OldBrief.Compile("left stop  right stop"));
        }

        #endregion Demo 3

        #region Demo 4 - Pololu Gyro

        static async Task Demo4()
        {
            var gyro = new Gyro(mcu, "gyro");
            await gyro.Initialize();

            for (var i = 0; i < 10; i++)
            {
                Console.WriteLine("Gyro: {0}", await gyro.AngularSpeedRaw());
                await Task.Delay(100);
            }

            var test = 0.0;
            for (var i = 0; i < 10; i++)
            {
                var speed = await gyro.AngularSpeedDps();
                test += speed;
                Console.WriteLine("GyroDps: {0} ({1})", speed, test);
                await Task.Delay(100);
            }

            await mcu.Execute(OldBrief.Compile("[42 gyroDps event 100 delay] setLoop")); // TODO: dangerous!!
            await Task.Delay(1000);
            await mcu.Execute(OldBrief.Compile("stopLoop"));
        }

        #endregion Demo 4

        #region Demo 5 - Pololu Accelerometer

        static async Task Demo5()
        {
            var accelerometer = new Accelerometer(mcu, "acceleration");
            await accelerometer.Initialize();

            for (var i = 0; i < 1000; i++)
            {
                Console.WriteLine("Acceleration: {0}", await accelerometer.Acceleration());
                await Task.Delay(100);
            }

            await mcu.Execute(OldBrief.Compile("[42 acceleration event 100 delay] setLoop")); // TODO: dangerous!!
            await Task.Delay(1000);
            await mcu.Execute(OldBrief.Compile("stopLoop"));
        }

        #endregion Demo 5

        #region Demo 6 - Balancing

        static async Task Demo6()
        {
            var balancing = new Balancing(mcu);
            await balancing.Initialize();

            var lastY = float.NaN;
            var lastX = float.NaN;
            var lastStart = false;
            var lastA = false;

#if SHORT_BOT
            var parameters = new Tuple<string, double>[] {
                new Tuple<string, double>("MinLeft", 30),
                new Tuple<string, double>("MinRight", 20),
                new Tuple<string, double>("SPD", 0),
                new Tuple<string, double>("Trim", -0.3),
                new Tuple<string, double>("PA", 30),
                new Tuple<string, double>("IA", 0.95),
                new Tuple<string, double>("PM", 0.9),
                new Tuple<string, double>("IM", 0.01),
                new Tuple<string, double>("DM", 10) };
#else
            var parameters = new Tuple<string, double>[] {
                new Tuple<string, double>("MinLeft", 20),
                new Tuple<string, double>("MinRight", 20),
                new Tuple<string, double>("SPD", 0),
                new Tuple<string, double>("Trim", 0.6),
                new Tuple<string, double>("PA", 30),
                new Tuple<string, double>("IA", 0.95),
                new Tuple<string, double>("PM", 0.9),
                new Tuple<string, double>("IM", 0.01),
                new Tuple<string, double>("DM", 10) };
#endif
            var selected = -1;
            GamePadState pad;

            foreach (var p in parameters)
                await mcu.Execute(OldBrief.Compile("{0} set{1}", (int)(p.Item2 * 1000 + 0.5), p.Item1));

            bool allowParameterTweaks = false;
            bool exit = false;
            var dampenDrive = 1.0f;

            do
            {
                pad = GamePad.GetState(PlayerIndex.One);

                var y = pad.ThumbSticks.Left.Y;

                const double easingThreshold = 0.2;
                if (Math.Abs(y) < easingThreshold)
                    dampenDrive = 1.0f; // turn off dampening once user eases off
                y *= dampenDrive;
                dampenDrive *= (Math.Abs(y) > easingThreshold ? 0.95f : 1.0f); // increase dampening until eased off

                if (y.CompareTo(lastY) != 0)
                {
                    lastY = y;
                    Console.WriteLine("Y: {0}", y);
#if SHORT_BOT
                    var d = y * (float)(y > 0 ? 2.75 : 3.5);
#else
                    var d = y * (float)(y > 0 ? 1.75 : 1.5);
#endif
                    await balancing.SetDrive(d);
                    //Console.WriteLine("Drive: {0}", d);
                }

                var x = pad.ThumbSticks.Left.X;
                if (x.CompareTo(lastX) != 0)
                {
                    lastX = x;
                    var t = x * 50;
#if !SHORT_BOT
                    t = -t; // motors reversed
#endif
                    await balancing.SetTurn(t);
                    Console.WriteLine("Turn: {0}", t);
                }

                if (allowParameterTweaks)
                {
                    var s = pad.IsButtonDown(Buttons.Start);
                    if (s && !lastStart)
                    {
                        await balancing.ToggleBalance();
                        Console.WriteLine("Toggle balancing");
                    }
                    lastStart = s;

                    var a = pad.IsButtonDown(Buttons.A);
                    if (a && !lastA)
                    {
                        selected = (selected + 1) % parameters.Length;
                        Console.WriteLine("Selected: {0}", parameters[selected]);
                    }
                    lastA = a;

                    if (pad.IsButtonDown(Buttons.X))
                    {
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            var n = parameters[i].Item1;
                            parameters[i] = new Tuple<string, double>(n, 0);
                            await mcu.Execute(OldBrief.Compile("0 set{0}", n));
                        }
                        Console.WriteLine("Reset parameters");
                    }

                    var adjust = 0.0;
                    var fine = pad.IsButtonDown(Buttons.Y);
                    if (pad.IsButtonDown(Buttons.DPadUp))
                        adjust = fine ? 0.01 : 0.1;
                    if (pad.IsButtonDown(Buttons.DPadDown))
                        adjust = fine ? -0.01 : -0.1;


                    if (adjust != 0.0 && selected != -1)
                    {
                        var p = parameters[selected];
                        var n = p.Item1;
                        var v = p.Item2 + adjust;
                        parameters[selected] = new Tuple<string, double>(n, v);
                        await mcu.Execute(OldBrief.Compile("{0} set{1}", (int)(v * 1000 + 0.5), n));
                        Console.WriteLine("{0} = {1}", n, v);
                        await Task.Delay(100);
                    }

                    if (pad.IsButtonDown(Buttons.Back))
                        exit = true;
                }

                if (pad.IsButtonDown(Buttons.LeftShoulder) &&
                    pad.IsButtonDown(Buttons.RightShoulder) &&
                    pad.Triggers.Left == 1 &&
                    pad.Triggers.Right == 1 &&
                    pad.IsButtonDown(Buttons.LeftStick) &&
                    pad.IsButtonDown(Buttons.B))
                {
                    allowParameterTweaks = !allowParameterTweaks;
                    Console.WriteLine("Allow Parameter Tweaks: {0}", allowParameterTweaks);
                    Thread.Sleep(500);
                }

                Thread.Sleep(10);
            } while (!exit);
        }

        #endregion Demo 6
        */
        #region Demo 7 - Servo
        static async Task Demo7()
        {
            var pan = new Servo(mcu, 1, 1000, 2000);
            await pan.Initialize();

            var tilt = new Servo(mcu, 0, 1000, 2000);
            await tilt.Initialize();

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
            } while (true);
        }

        #endregion Demo 7

        static async Task Demos()
        {
            try
            {
                Console.WriteLine("Demo #1 - Basic LEDs, etc.");
                await Demo1(); // Basic LEDs, etc.
                Console.WriteLine("Demo #2 - Custom heartbeat");
                //await Demo2(); // Custom heartbeat
                Console.WriteLine("Demo #3 - MonsterMoto driver");
                //await Demo3(); // MonsterMoto driver
                Console.WriteLine("Demo #4 - Pololu Gyro");
                //await Demo4(); // Pololu Gyro
                Console.WriteLine("Demo #5 - Pololu Accelermeter");
                //await Demo5(); // Pololu Accelermeter
                Console.WriteLine("Demo #6 - Balancing");
                //await Demo6(); // Balancing
                Console.WriteLine("Demo #7 - Servo");
                await Demo7(); // Servo
                Console.WriteLine("Disconnecting...");
                await Disconnect();
                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Connecting...");
            Connect();
            Demos();
            Console.ReadLine();
        }
    }
}