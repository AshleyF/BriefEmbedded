using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Robotics.Brief;

namespace Microsoft.Robotics.Microcontroller
{
    /// <summary>
    /// Microcontroller data event arguments.
    /// </summary>
    public class DataEventArgs : EventArgs
    {
        /// <summary>
        /// Event ID.
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// Event value.
        /// </summary>
        public IEnumerable<byte> Value { get; private set; }

        /// <summary>
        /// Construct new instance of DataEventArgs.
        /// </summary>
        /// <param name="id">Event ID.</param>
        /// <param name="value">Event payload.</param>
        public DataEventArgs(int id, byte[] value)
        {
            ID = id;
            Value = value;
        }
    }

    /// <summary>
    /// Microcontroller data event handler.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event args.</param>
    public delegate void DataEventHandler(object sender, DataEventArgs e);

    /// <summary>
    /// Microcontroller protocol event arguments.
    /// </summary>
    public class ProtocolEventArgs : EventArgs
    {
        /// <summary>
        /// Whether this indicates an unrecoverable condition.
        /// </summary>
        public bool Fatal { get; private set; }

        /// <summary>
        /// Warning or error message text.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Construct new instance of ProtocolEventArgs.
        /// </summary>
        /// <param name="message">Message text.</param>
        /// <param name="fatal">Indication of whether considered fatal.</param>
        public ProtocolEventArgs(string message, bool fatal)
        {
            Message = message;
            Fatal = fatal;
        }
    }

    /// <summary>
    /// Microcontroller protocol event handler.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event args.</param>
    public delegate void ProtocolEventHandler(object sender, ProtocolEventArgs e);

    /// <summary>
    /// Microcontroller Hal is the minimum set of functionality a
    /// microcontroller control board must implement to work within our system.
    /// </summary>
    public interface IMicrocontrollerHal
    {
        /// <summary>
        /// Connect to microcontroller.
        /// </summary>
        /// <returns>Task completes when connection established.</returns>
        Task Connect();

        /// <summary>
        /// Disconnect from microcontroller.
        /// </summary>
        /// <returns>Task completes when connection has been closed.</returns>
        Task Disconnect();

        /// <summary>
        /// Allocate event ID and bind callback function.
        /// </summary>
        /// <param name="callback">Callback envoked upon event ID.</param>
        /// <returns>Event ID</returns>
        int AllocateEventId(Action<byte[]> callback);

        /// <summary>
        /// Execute bytecode instructions immediately.
        /// </summary>
        /// <param name="code">Bytecode instruction sequence.</param>
        /// <returns>Task completes when bytecode has been sent.</returns>
        Task Execute(IEnumerable<byte> code);

        /// <summary>
        /// Add bytecode sequence to the microcontroller dictionary.
        /// </summary>
        /// <param name="code">Bytecode instruction sequence.</param>
        /// <returns>Task completes when bytecode has been sent.</returns>
        Task<int> Define(IEnumerable<byte> code);

        /// <summary>
        /// Data event.
        /// </summary>
        event DataEventHandler Data;

        /// <summary>
        /// Protocol event (warning/error).
        /// </summary>
        event ProtocolEventHandler Protocol;
    }

    public delegate void TransportEventHandler(object sender, EventArgs args);

    public interface ITransport
    {
        void Connect();
        bool BytesAvailable { get; }
        byte ReadByte();
        void WriteByte(byte b);
        void Flush();
        void Disconnect();
        event TransportEventHandler DataReceived;
    }

    public class SerialTransport : ITransport
    {
        private readonly SerialPort port;

        public SerialTransport(string portName)
        {
            port = new SerialPort(portName);
            port.DataReceived += OnDataReceived;
            port.BaudRate = 9600;
            port.ReadTimeout = 10000;
            port.WriteTimeout = 10000;
        }

        public void Connect()
        {
            if (!port.IsOpen)
                port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
        }

        public bool BytesAvailable
        {
            get { return port.BytesToRead > 0; }
        }

        public byte ReadByte()
        {
            return (byte)port.ReadByte();
        }

        public void WriteByte(byte b)
        {
            port.BaseStream.WriteByte(b);
        }

        public void Flush()
        {
            port.BaseStream.Flush();
        }

        public void Disconnect()
        {
            if (port.IsOpen)
                port.Close();
        }

        public event TransportEventHandler DataReceived;

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType == SerialData.Chars && port.BytesToRead > 0 && DataReceived != null)
                DataReceived(this, EventArgs.Empty);
        }
    }

    public class MicrocontrollerHal : IMicrocontrollerHal, IDisposable
    {
        protected Compiler compiler;

        private ITransport transport;

        public MicrocontrollerHal(ITransport transport)
        {
            compiler = new Compiler();
            transport.DataReceived += InputReceived;
            this.transport = transport;
        }

        public void Dispose()
        {
            transport.Disconnect();
            GC.SuppressFinalize(this);
        }

        private Dictionary<int, Action<byte[]>> eventListeners = new Dictionary<int, Action<byte[]>>();

        public event DataEventHandler Data;

        private void OnData(int id, byte[] value)
        {
            Action<byte[]> callback;
            if (eventListeners.TryGetValue(id, out callback))
                callback(value);

            if (Data != null)
                Data(this, new DataEventArgs(id, value));
        }

        public event ProtocolEventHandler Protocol;

        private void OnProtocol(string message, bool fatal)
        {
            if (Protocol != null)
                Protocol(this, new ProtocolEventArgs(message, fatal));
        }

        private void InputReceived(object sender, EventArgs e)
        {
            lock (transport)
            {
                Monitor.Pulse(transport);
            }
        }

        private Thread outputThread, inputThread;

        public async Task Connect()
        {
            outputThread = new Thread(new ThreadStart(ProcessOutput));
            outputThread.Name = "Microcontroller output";
            outputThread.IsBackground = true;
            outputThread.Start();

            inputThread = new Thread(new ThreadStart(ProcessInput));
            inputThread.Name = "Microcontroller input";
            inputThread.IsBackground = true;
            inputThread.Start();

            transport.Connect();
            
            await RemoteReset();
            LocalReset();
        }

        public async Task Disconnect()
        {
            await RemoteReset();
            LocalReset();

            inputThread.Abort();
            outputThread.Abort();
            await Task.Delay(1000); // give threads a chance to die

            lock (transport)
            {
                transport.Disconnect();
            }

            await Task.Delay(1000); // give port a chance to close
        }

        const byte END            = 0xC0;
        const byte ESCAPE         = 0xDB;
        const byte ESCAPED_END    = 0xDC;
        const byte ESCAPED_ESCAPE = 0xDD;

        private byte outputCrc = 0;

        private void WriteEscaped(byte b)
        {
            switch (b)
            {
                case END:
                    transport.WriteByte(ESCAPE);
                    transport.WriteByte(ESCAPED_END);
                    break;
                case ESCAPE:
                    transport.WriteByte(ESCAPE);
                    transport.WriteByte(ESCAPED_ESCAPE);
                    break;
                default:
                    transport.WriteByte(b);
                    break;
            }
            outputCrc ^= b;
        }

        byte outputSequence = 0;

        private void WriteHeader()
        {
            outputCrc = 0;
            WriteEscaped(outputSequence++);
        }

        private void WriteFooter()
        {
            WriteEscaped(outputCrc);
            transport.WriteByte(END);
            transport.Flush();
        }

        private Queue<Tuple<bool, IEnumerable<byte>, TaskCompletionSource<int>>> outputQueue = new Queue<Tuple<bool, IEnumerable<byte>, TaskCompletionSource<int>>>();

        private void ProcessOutput()
        {
            while (true)
            {
                try
                {
                    lock (outputQueue)
                    {
                        if (outputQueue.Count == 0)
                            Monitor.Wait(outputQueue);
                        var msg = outputQueue.Dequeue();
                        var define = msg.Item1;
                        var code = msg.Item2;
                        var tcs = msg.Item3;

                        int type = define ? 1 : 0;
                        int codeSize = 0;
                        lock (transport)
                        {
                            WriteHeader();
                            foreach (var b in code)
                            {
                                WriteEscaped(b);
                                codeSize++;
                            }
                            WriteEscaped((byte)type);
                            WriteFooter();
                        }
                        tcs.SetResult(codeSize);
                    }
                }
                catch (TimeoutException)
                {
                    OnProtocol("Output timeout", false);
                }
                catch (ThreadAbortException)
                {
                    // expected upon closing
                }
                catch (Exception ex)
                {
                    OnProtocol(ex.Message, true);
                    break;
                }
            }
        }

        private Task<int> WriteMessage(bool define, IEnumerable<byte> code)
        {
            var tcs = new TaskCompletionSource<int>();
            lock (outputQueue)
            {
                outputQueue.Enqueue(new Tuple<bool, IEnumerable<byte>, TaskCompletionSource<int>>(define, code, tcs));
                Monitor.Pulse(outputQueue);
            }
            return tcs.Task;
        }

        private class ProtocolException : Exception
        {
            public ProtocolException(string message, params object[] args)
                : base(string.Format(message, args))
            {
            }

            public ProtocolException(string message)
                : base(message)
            {
            }
        }

        private byte inputCrc = 0;

        private bool ReadUnescaped(out byte b)
        {
            b = transport.ReadByte();
            if (b == END)
            {
                return false;
            }
            if (b == ESCAPE)
            {
                switch (transport.ReadByte())
                {
                    case ESCAPED_END:
                        b = END;
                        break;
                    case ESCAPED_ESCAPE:
                        b = ESCAPE;
                        break;
                    default:
                        throw new ProtocolException("Local - Unexpected escape value.");
                }
            }
            inputCrc ^= b;
            return true;
        }

        private byte ReadUnescaped()
        {
            byte b;
            if (!ReadUnescaped(out b))
                throw new ProtocolException("Local - Unexpected end.");
            return b;
        }

        private byte inputSequence = 0xFF;

        private bool coldStart = true;

        private void ProcessInput()
        {
            while (true)
            {
                lock (transport)
                {
                    try
                    {
                        Monitor.Wait(transport);
                        if (transport.BytesAvailable)
                        {
                            if (coldStart) // failed frame (reading to next end)
                            {
                                byte b;
                                while (ReadUnescaped(out b)) ;
                                coldStart = false;
                            }
                            else
                            {
                                inputCrc = 0;
                                var seq = ReadUnescaped();
                                if (seq != ++inputSequence)
                                {
                                    OnProtocol(string.Format("Local - Out of sequence frame ({0})", seq), false);
                                    inputSequence = seq;
                                }
                                var id = ReadUnescaped();
                                switch (id)
                                {
                                    case 0xFC: // vm warning/error
                                        var val = ReadUnescaped();
                                        byte b;
                                        ReadUnescaped(out b); // CRC (side effect of updating inputCrc which should be zero now)
                                        ReadUnescaped(out b); // END
                                        switch (val)
                                        {
                                            case 0:
                                                throw new ProtocolException("Remote - Return stack underflow");
                                            case 1:
                                                throw new ProtocolException("Remote - Return stack overflow");
                                            case 2:
                                                throw new ProtocolException("Remote - Data stack underflow");
                                            case 3:
                                                throw new ProtocolException("Remote - Data stack overflow");
                                            case 4:
                                                throw new ProtocolException("Remote - Indexed out of memory");
                                            default:
                                                throw new ProtocolException("Remote - Unknown VM error");
                                        }
                                    case 0xFD: // protocol message
                                        throw new NotImplementedException("Protocol messages not supported");
                                    case 0xFE: // protocol warning/error
                                        val = ReadUnescaped();
                                        ReadUnescaped(out b); // CRC (side effect of updating inputCrc which should be zero now)
                                        ReadUnescaped(out b); // END
                                        switch (val)
                                        {
                                            case 0:
                                                OnProtocol("Remote - Out of sequence frame", false);
                                                break;
                                            case 1:
                                                throw new ProtocolException("Remote - Unexpected escape");
                                            case 2:
                                                throw new ProtocolException("Remote - CRC failure");
                                            case 3:
                                                throw new ProtocolException("Remote - Unexpected end.");
                                            case 4:
                                                throw new ProtocolException("Remote - Buffer overflow.");
                                            default:
                                                throw new ProtocolException("Remote - Unknown protocol error");
                                        }
                                        break;
                                    case 0xFF: // board reset
                                        ReadUnescaped(out b); // CRC (side effect of updating inputCrc which should be zero now)
                                        ReadUnescaped(out b); // END
                                        LocalReset();
                                        break;
                                    default: // user event
                                        var data = new List<byte>();
                                        while (ReadUnescaped(out b))
                                            data.Add(b);
                                        OnData(id, data.ToArray());
                                        break;
                                }
                                if (inputCrc != 0)
                                {
                                    coldStart = true;
                                    throw new ProtocolException("Local - CRC failure ({0})", inputCrc);
                                }
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        OnProtocol("Input timeout", false);
                    }
                    catch (ThreadAbortException)
                    {
                        // expected upon closing
                    }
                    catch (Exception ex)
                    {
                        OnProtocol(ex.Message, false);
                    }
                }
            }
        }

        int address = 0;

        protected void LocalReset()
        {
            inputSequence = 0xFF;
            outputSequence = 0;
            coldStart = true;
            address = 0;
        }

        protected async Task RemoteReset()
        {
            // TODO: Compile this
            await Execute(new byte[] { 56 }); // RESET
        }

        private byte availableId = 239; // 240+ reserved for VM and protocol events

        public int AllocateEventId(Action<byte[]> callback)
        {
            if (availableId < 0)
                throw new InvalidOperationException("Event IDs have been exhausted.");

            var id = availableId--;
            eventListeners.Add(id, callback);

            return id;
        }

        private void Trace(string label, IEnumerable<byte> code)
        {
            //
            var bytes = code.ToArray();
            Console.WriteLine(
                "{0}:{1}\nBytecode ({2}): {3}\n",
                label,
                compiler.Disassemble(bytes),
                bytes.Length,
                bytes.Select(b => string.Format("{0:x2} ", b)).Aggregate((a, b) => a + b));
            //*/
        }

        public async Task Execute(IEnumerable<byte> code)
        {
            Trace("Execute", code);
            await WriteMessage(false, code);
        }

        public async Task<int> Define(IEnumerable<byte> code)
        {
            Trace("Define", code);
            var word = address;
            address += (short)(await WriteMessage(true, code));
            return word;
        }
    }
}