using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Robotics.Brief;

namespace Microsoft.Robotics.Microcontroller
{
    /// <summary>
    /// Low level interface for talking directly to the microcontroller to call functions, define scripts, etc.
    /// </summary>
    public interface IMicrocontroller : IMicrocontrollerHal
    {
        Task ExecuteBrief(string brief);
        Task ExecuteBrief(string brief, params int[] args);

        Task ExecuteMethod(MethodInfo meth);
        Task ExecuteAction(Action meth);
        Task ExecuteAction(Action<int> meth);
        Task ExecuteAction(Action<int, int> meth);
        Task ExecuteAction(Action<int, int, int> meth);
        Task ExecuteAction(Action<int, int, int, int> meth);
        Task ExecuteFunc(Func<int> meth);
        Task ExecuteFunc(Func<int, int> meth);
        Task ExecuteFunc(Func<int, int, int> meth);
        Task ExecuteFunc(Func<int, int, int, int> meth);
        Task ExecuteFunc(Func<int, int, int, int, int> meth);

        Task ExecuteMethod(MethodInfo meth, params int[] args);
        Task ExecuteAction(Action meth, params int[] args);
        Task ExecuteAction(Action<int> meth, params int[] args);
        Task ExecuteAction(Action<int, int> meth, params int[] args);
        Task ExecuteAction(Action<int, int, int> meth, params int[] args);
        Task ExecuteAction(Action<int, int, int, int> meth, params int[] args);
        Task ExecuteFunc(Func<int> meth, params int[] args);
        Task ExecuteFunc(Func<int, int> meth, params int[] args);
        Task ExecuteFunc(Func<int, int, int> meth, params int[] args);
        Task ExecuteFunc(Func<int, int, int, int> meth, params int[] args);
        Task ExecuteFunc(Func<int, int, int, int, int> meth, params int[] args);

        void DefineBrief(string word, string brief);
        void DefineBrief(string word, string brief, params int[] args);

        void DefineMethod(MethodInfo meth);
        void DefineAction(Action meth);
        void DefineAction(Action<int> meth);
        void DefineAction(Action<int, int> meth);
        void DefineAction(Action<int, int, int> meth);
        void DefineAction(Action<int, int, int, int> meth);
        void DefineFunc(Func<int> meth);
        void DefineFunc(Func<int, int> meth);
        void DefineFunc(Func<int, int, int> meth);
        void DefineFunc(Func<int, int, int, int> meth);
        void DefineFunc(Func<int, int, int, int, int> meth);

        void DefineBrief(string word, MethodInfo meth, string brief);

        void BindMethod(string brief, MethodInfo meth);
        void BindAction(string brief, Action meth);
        void BindAction(string brief, Action<int> meth);
        void BindAction(string brief, Action<int, int> meth);
        void BindAction(string brief, Action<int, int, int> meth);
        void BindAction(string brief, Action<int, int, int, int> meth);
        void BindFunc(string brief, Func<int> meth);
        void BindFunc(string brief, Func<int, int> meth);
        void BindFunc(string brief, Func<int, int, int> meth);
        void BindFunc(string brief, Func<int, int, int, int> meth);
        void BindFunc(string brief, Func<int, int, int, int, int> meth);

        void BindField(string name, FieldInfo field);

        void Instruction(string word, byte code);
    }

    public class Microcontroller : MicrocontrollerHal, IMicrocontroller, IDisposable
    {
        public Microcontroller(ITransport transport)
            : base(transport)
        {
            base.Data += OnData;
        }

        private string PrependArgs(string brief, int[] args)
        {
            if (args.Length == 0)
                return brief;
            return args.Select(x => x.ToString() + " ").Aggregate((x, y) => x + y) + brief;
        }

        public async Task Reset()
        {
            await RemoteReset();
            LocalReset();
            compiler.Reset();
        }

        private async Task<byte[]> Code(Tuple<byte[], byte[]> code)
        {
            var exe = code.Item1;
            var def = code.Item2;
            if (def.Count() > 0)
                await Define(def);
            return exe;
        }

        private async Task PushArgs(int[] args)
        {
            if (args.Length > 0)
                await ExecuteBrief(string.Empty, args); // push args
        }

        public async Task ExecuteBrief(string brief)
        {
            await Execute(await Code(compiler.EagerCompile(brief)));
        }

        public async Task ExecuteBrief(string brief, params int[] args)
        {
            await ExecuteBrief(PrependArgs(brief, args));
        }

        public async Task ExecuteMethod(MethodInfo meth)
        {
            if (meth.GetParameters().Length > 0)
                throw new ArgumentException(string.Format("Arguments missing ({0})", meth.Name));
            await Execute(await Code(compiler.EagerTranslate(meth)));
        }

        public async Task ExecuteAction(Action meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteAction(Action<int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteAction(Action<int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteAction(Action<int, int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteAction(Action<int, int, int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteFunc(Func<int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteFunc(Func<int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteFunc(Func<int, int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteFunc(Func<int, int, int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteFunc(Func<int, int, int, int, int> meth)
        {
            await ExecuteMethod(meth.GetMethodInfo());
        }

        public async Task ExecuteMethod(MethodInfo meth, params int[] args)
        {
            if (args.Length != meth.GetParameters().Length)
                throw new ArgumentException(string.Format("Argument mismatch ({0})", meth.Name));
            await PushArgs(args);
            await Execute(await Code(compiler.EagerTranslate(meth)));
        }

        public async Task ExecuteAction(Action meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteAction(Action<int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteAction(Action<int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteAction(Action<int, int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteAction(Action<int, int, int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteFunc(Func<int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteFunc(Func<int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteFunc(Func<int, int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteFunc(Func<int, int, int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public async Task ExecuteFunc(Func<int, int, int, int, int> meth, params int[] args)
        {
            await ExecuteMethod(meth.GetMethodInfo(), args);
        }

        public void DefineBrief(string word, string brief)
        {
            compiler.Define(word, compiler.LazyCompile(brief));
        }

        public void DefineBrief(string word, string brief, params int[] args)
        {
            DefineBrief(word, string.Format(brief, args));
        }

        public void DefineMethod(MethodInfo meth)
        {
            compiler.Define(meth.Name, meth, compiler.LazyTranslate(meth));
        }

        public void DefineAction(Action meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineAction(Action<int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineAction(Action<int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineAction(Action<int, int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineAction(Action<int, int, int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineFunc(Func<int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineFunc(Func<int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineFunc(Func<int, int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineFunc(Func<int, int, int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineFunc(Func<int, int, int, int, int> meth)
        {
            DefineMethod(meth.GetMethodInfo());
        }

        public void DefineBrief(string word, MethodInfo meth, string brief)
        {
            compiler.Define(word, meth, compiler.LazyCompile(brief));
        }

        public void BindMethod(string brief, MethodInfo meth)
        {
            compiler.Define(meth.Name, meth, compiler.LazyCompile(brief));
        }

        public void BindAction(string brief, Action meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindAction(string brief, Action<int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindAction(string brief, Action<int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindAction(string brief, Action<int, int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindAction(string brief, Action<int, int, int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindFunc(string brief, Func<int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindFunc(string brief, Func<int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindFunc(string brief, Func<int, int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindFunc(string brief, Func<int, int, int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindFunc(string brief, Func<int, int, int, int, int> meth)
        {
            BindMethod(brief, meth.GetMethodInfo());
        }

        public void BindField(string name, FieldInfo field)
        {
            compiler.Define(name, field, compiler.LazyCompile("[(return)]"));
        }

        public void Instruction(string word, byte code)
        {
            compiler.Instruction(word, code);
        }

        private List<Tuple<int, Action<int>>> awaitingRead = new List<Tuple<int, Action<int>>>();

        private void OnData(object sender, DataEventArgs args)
        {
            lock (awaitingRead)
            {
                foreach (var a in awaitingRead.FindAll(a => a.Item1 == args.ID))
                {
                    var value = 0;
                    foreach (var b in args.Value)
                        value = (value << 8) + b;
                    a.Item2(value);
                    awaitingRead.Remove(a);
                }
            }
        }

        public const int High = -1;
        public const int True = -1;
        public const int On   = -1;

        public const int Low   = 0;
        public const int False = 0;
        public const int Off   = 0;

        public const int Input  = 0;
        public const int Output = 1;

        public static void PinMode(int mode, int pin)
        {
            throw new NotImplementedException();
        }

        public static void DigitalWrite(int value, int pin)
        {
            throw new NotImplementedException();
        }

        public static int DigitalRead(int pin)
        {
            throw new NotImplementedException();
        }

        public static void AnalogWrite(int value, int pin)
        {
            throw new NotImplementedException();
        }

        public static int AnalogRead(int pin)
        {
            throw new NotImplementedException();
        }

        public static int LoopTicks()
        {
            throw new NotImplementedException();
        }

        public static void WireBegin()
        {
            throw new NotImplementedException();
        }

        public static void WireRequestFrom(int numBytes, int device)
        {
            throw new NotImplementedException();
        }

        public static int WireAvailable()
        {
            throw new NotImplementedException();
        }

        public static int WireRead()
        {
            throw new NotImplementedException();
        }

        public static void WireBeginTransmission(int device)
        {
            throw new NotImplementedException();
        }

        public static void WireWrite(int value)
        {
            throw new NotImplementedException();
        }

        public static void WireEndTransmission()
        {
            throw new NotImplementedException();
        }

        public static void ServoAttach(int pin)
        {
            throw new NotImplementedException();
        }

        public static void ServoDetach(int pin)
        {
            throw new NotImplementedException();
        }

        public static void ServoWriteMicros(int micros, int pin)
        {
            throw new NotImplementedException();
        }

        public static int Milliseconds()
        {
            throw new NotImplementedException();
        }

        public static int PulseIn(int value, int pin)
        {
            throw new NotImplementedException();
        }

        public static void Initialize(IMicrocontroller mcu)
        {
            mcu.BindAction("pinMode"              , PinMode);
            mcu.BindFunc  ("digitalRead"          , DigitalRead);
            mcu.BindAction("digitalWrite"         , DigitalWrite);
            mcu.BindFunc  ("analogRead"           , AnalogRead);
            mcu.BindAction("analogWrite"          , AnalogWrite);
            mcu.BindFunc  ("loopTicks"            , LoopTicks);
            mcu.BindAction("wireBegin"            , WireBegin);
            mcu.BindAction("wireRequestFrom"      , WireRequestFrom);
            mcu.BindFunc  ("wireAvailable"        , WireAvailable);
            mcu.BindFunc  ("WireRead"             , WireRead);
            mcu.BindAction("WireBeginTransmission", WireBeginTransmission);
            mcu.BindAction("WireWrite"            , WireWrite);
            mcu.BindAction("WireEndTransmission"  , WireEndTransmission);
            mcu.BindAction("ServoAttach"          , ServoAttach);
            mcu.BindAction("ServoDetach"          , ServoDetach);
            mcu.BindAction("ServoWriteMicros"     , ServoWriteMicros);
            mcu.BindFunc  ("Milliseconds"         , Milliseconds);
            mcu.BindFunc  ("PulseIn"              , PulseIn);
        }
    }
}