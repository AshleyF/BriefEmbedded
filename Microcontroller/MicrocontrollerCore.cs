using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Robotics
{
    public class DataEventArgs : IDataEventArgs
    {
        public int ID { get; protected set; }
        public byte[] Value { get; private set; }

        public DataEventArgs(int id, byte[] value)
        {
            ID = id;
            Value = value;
        }
    }

    public class ProtocolEventArgs : IProtocolEventArgs
    {
        public string Message { get; private set; }
        public bool Fatal { get; private set; }

        public ProtocolEventArgs(string message, bool fatal)
        {
            Message = message;
            Fatal = fatal;
        }
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
            port.BaudRate = 115200;
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

    public class MicrocontrollerCore : IMicrocontrollerCore, IDisposable
    {
        private ITransport transport;

        public MicrocontrollerCore(ITransport transport)
        {
            transport.DataReceived += InputReceived;
            this.transport = transport;
        }

        public void Dispose()
        {
            transport.Disconnect();
            GC.SuppressFinalize(this);
        }

        public event DataEventHandler Data;

        private void OnData(int id, byte[] value)
        {
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

        const byte START          = 0xC0;
        const byte ESCAPE         = 0xDB;
        const byte ESCAPED_START  = 0xDC;
        const byte ESCAPED_ESCAPE = 0xDD;

        private byte outputCrc = 0;

        private void WriteEscaped(byte b)
        {
            switch (b)
            {
                case START:
                    transport.WriteByte(ESCAPE);
                    transport.WriteByte(ESCAPED_START);
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

        private void WriteHeader(byte length)
        {
            transport.WriteByte(START);
            outputCrc = 0;
            WriteEscaped(outputSequence++);
            WriteEscaped(length);
        }

        private void WriteFooter()
        {
            WriteEscaped(outputCrc);
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

                        int size = code.Count(); // TODO: Count forces double pass
                        if (size > Byte.MaxValue)
                            throw new InvalidCastException("Exceeded maximum code size.");
                        lock (transport)
                        {
                            WriteHeader((byte)size);
                            WriteEscaped((byte)(define ? 1 : 0));
                            foreach (var b in code)
                                WriteEscaped(b);
                            WriteFooter();
                        }
                        tcs.SetResult(size);
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

        private byte ReadUnescaped()
        {
            var b = transport.ReadByte();
            if (b == ESCAPE)
            {
                switch (transport.ReadByte())
                {
                    case ESCAPED_START:
                        b = START;
                        break;
                    case ESCAPED_ESCAPE:
                        b = ESCAPE;
                        break;
                    default:
                        throw new ProtocolException("Local - Unexpected escape value.");
                }
            }
            inputCrc ^= b;
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
                            if (coldStart) // failed frame (reading to next start)
                            {
                                while (ReadUnescaped() != START) ;
                                coldStart = false;
                            }
                            else
                            {
                                if (ReadUnescaped() != START)
                                    throw new ProtocolException("Local - Expected start byte.");
                            }
                            inputCrc = 0;
                            var seq = ReadUnescaped();
                            if (seq != ++inputSequence)
                            {
                                OnProtocol(string.Format("Local - Out of sequence frame ({0})", seq), false);
                                inputSequence = seq;
                            }
                            var len = ReadUnescaped();
                            var id = ReadUnescaped();
                            switch (id)
                            {
                                case 0xFD: // vm warning/error
                                    var val = (len == 1 ? 0 : ReadUnescaped());
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
                                case 0xFE: // protocol warning/error
                                    val = (len == 1 ? 0 : ReadUnescaped());
                                    switch (val)
                                    {
                                        case 0:
                                            OnProtocol("Remote - Out of sequence frame", false);
                                            break;
                                        case 1:
                                            throw new ProtocolException("Remote - Unexpected escape");
                                        case 2:
                                            throw new ProtocolException("Remote - Expected start");
                                        case 3:
                                            throw new ProtocolException("Remote - CRC failure");
                                        default:
                                            throw new ProtocolException("Remote - Unknown protocol error");
                                    }
                                    break;
                                case 0xFF: // board reset
                                    LocalReset();
                                    break;
                                default: // user event
                                    var data = new byte[len - 1];
                                    for (var i = 0; i < data.Length; i++)
                                        data[i] = ReadUnescaped();
                                    OnData(id, data);
                                    break;
                            }
                            ReadUnescaped(); // CRC (side effect of updating inputCrc which should be zero now)
                            if (inputCrc != 0)
                            {
                                coldStart = true;
                                throw new ProtocolException("Local - CRC failure ({0})", inputCrc);
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
            await Execute(new byte[] { 0x80 }); // RESET
        }

        public async Task Execute(IEnumerable<byte> code)
        {
            //Console.WriteLine("Execute {0} bytes", code.Count());
            await WriteMessage(false, code);
        }

        public async Task<int> Define(IEnumerable<byte> code)
        {
            //Console.WriteLine("Define {0} bytes", code.Count());
            var word = address;
            address += (short)(await WriteMessage(true, code)) + 1; // +1 for implied return instruction
            return word;
        }
    }
}