//------------------------------------------------------------------------------
//  <copyright file="ReflectaClient.cs" company="Microsoft Corporation">
//      Copyright (C) Microsoft Corporation.  All rights reserved.
//  </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Robotics.Tests.Reflecta
{
    using System;
    using System.Diagnostics;
    using System.IO.Ports;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class ReflectaClient : IDisposable
    {
        private static readonly TraceSource traceSource = new TraceSource("ReflectaClient");
        private readonly SerialPort _port;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private byte _writeChecksum;
        private byte _writeSequence;
        private bool _escaped; // protocol parser escape state
        private byte _readChecksum;
        private ReadState _state = ReadState.WaitingForSequence; // protocol parser state
        private byte? _readSequence;
        private byte[] _frameBuffer;
        private byte _frameIndex;

        public ReflectaClient(string portName)
        {
            _port = new SerialPort(portName, 19200);
            _port.Open();

            Task.Factory.StartNew(() => {
                    while (!_cts.IsCancellationRequested)
                    {
                        ReflectaLoop();
                        Thread.Sleep(0);
                    }
                },
                _cts.Token);
        }

        public event EventHandler<MessageEventArgs> ErrorReceived;

        public event EventHandler<MessageEventArgs> MessageReceived;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        public event EventHandler<ResponseReceivedEventArgs> ResponseReceived;

        public enum FunctionId : byte
        {
            PushArray       = 0x00,
            QueryInterface  = 0x01,
            Response        = 0x7D,
            Message         = 0x7E,
            Error           = 0x7F
        }

        private enum ProtocolMessage : byte
        {
            OutOfSequence,
            UnexpectedEscape,
            CrcMismatch,
            UnexpectedEnd,
            BufferOverflow,
            FrameTooSmall,
            FunctionConflict,
            FunctionNotFound,
            ParameterMismatch,
            StackOverflow,
            StackUnderflow
        }

        // Packet format is:
        // Frame Sequence #, SLIP escaped
        // Byte(s) of Payload, SLIP escaped
        // CRC8 of Sequence # & Payload bytes, SLIP escaped
        // SLIP END (0xc0)
        private enum ReadState
        {
            /// <summary>
            /// Beginning of a new frame, waiting for the Sequence number
            /// </summary>
            WaitingForSequence,
            
            /// <summary>
            /// Reading data until an END character is found
            /// </summary>
            WaitingForBytecode,

            /// <summary>
            /// END character found, check CRC and deliver frame
            /// </summary>
            ProcessPayload,

            /// <summary>
            /// Current frame is invalid, wait for an END character and start parsing again
            /// </summary>
            WaitingForRecovery
        }

        /// <summary>
        /// Slip special characters
        /// </summary>
        private enum Slip : byte
        {
            /// <summary>
            /// Slip End
            /// </summary>
            End = 0xC0,

            /// <summary>
            /// Slip Escape
            /// </summary>
            Escape = 0xDB,

            /// <summary>
            /// Slip Escaped End
            /// </summary>
            EscapedEnd = 0xDC,

            /// <summary>
            /// Slip Escaped Escape
            /// </summary>
            EscapedEscape = 0xDD
        }

        public static TraceSource TraceSource
        {
            get
            {
                return traceSource;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _port.Dispose();
            _cts.Dispose();
        }

        public void SendFrame(byte[] frame)
        {
            _writeChecksum = 0;
            WriteEscaped(_writeSequence++);

            for (byte index = 0; index < frame.Length; index++)
            {
                WriteEscaped(frame[index]);
            }

            WriteEscaped(_writeChecksum);
            Write((byte)Slip.End);
        }

        private byte _sequence;

        // Read the uncoming data stream, to be called inside Arduino loop()
        private void ReflectaLoop()
        {
            while (Available())
            {
                byte b;
                if (ReadUnescaped(out b))
                {
                    switch (_state)
                    {
                        case ReadState.WaitingForRecovery:
                            break;
                        case ReadState.WaitingForSequence:
                            _sequence = b;

                            if (_readSequence == null)
                            {
                                _readSequence = _sequence;
                            }
                            else if (++_readSequence != _sequence)
                            {
                                Console.WriteLine("SEQ Expected {0} received {1}", _readSequence, _sequence);
                                _readSequence = _sequence;
                                if (ErrorReceived != null)
                                {
                                    ErrorReceived(this, new MessageEventArgs("Out of Sequence"));
                                }
                            }

                            _readChecksum = _sequence;
                            _frameIndex = 0; // Reset the buffer pointer to beginning
                            _frameBuffer = new byte[80];
                            _state = ReadState.WaitingForBytecode;
                            break;
                        case ReadState.WaitingForBytecode:
                            if (_frameIndex == _frameBuffer.Length)
                            {
                                if (ErrorReceived != null)
                                {
                                    ErrorReceived(this, new MessageEventArgs("Buffer Overflow"));
                                }

                                _state = ReadState.WaitingForRecovery;
                            }
                            else
                            {
                                _frameBuffer[_frameIndex++] = b;
                            }

                            break;
                        case ReadState.ProcessPayload:
                            // zero expected because finally XOR'd with itself
                            if (_readChecksum == 0)
                            {
                                _frameIndex--; // Remove the checksum byte from the frame data
                                if (_frameBuffer[0] == (byte)FunctionId.Error)
                                {
                                    Debug.Assert(_frameIndex == 2);
                                    if (ErrorReceived != null)
                                    {
                                        string message = "Teensy Unknown";
                                        switch (_frameBuffer[1])
                                        {
                                            case (byte)ProtocolMessage.OutOfSequence:
                                                message = "Teensy Out Of Sequence";
                                                break;
                                            case (byte)ProtocolMessage.UnexpectedEscape:
                                                message = "Teensy Unexpected Escape";
                                                break;
                                            case (byte)ProtocolMessage.CrcMismatch:
                                                message = "Teensy Crc Mismatch";
                                                break;
                                            case (byte)ProtocolMessage.UnexpectedEnd:
                                                message = "Teensy Unexpected End";
                                                break;
                                            case (byte)ProtocolMessage.BufferOverflow:
                                                message = "Teensy Buffer Overflow";
                                                break;
                                            case (byte)ProtocolMessage.FrameTooSmall:
                                                message = "Teensy Frame Too Small";
                                                break;
                                            case (byte)ProtocolMessage.FunctionConflict:
                                                message = "Teensy Function Conflict";
                                                break;
                                            case (byte)ProtocolMessage.FunctionNotFound:
                                                message = "Teensy Function Not Found";
                                                break;
                                            case (byte)ProtocolMessage.ParameterMismatch:
                                                message = "Teensy Parameter Mismatch";
                                                break;
                                            case (byte)ProtocolMessage.StackOverflow:
                                                message = "Teensy Stack Overflow";
                                                break;
                                            case (byte)ProtocolMessage.StackUnderflow:
                                                message = "Teensy Stack Underflow";
                                                break;
                                            default:
                                                break;
                                        }

                                        ErrorReceived(this, new MessageEventArgs(message));
                                    }
                                }
                                else if (_frameIndex > 1 && _frameBuffer[0] == (byte)FunctionId.Message)
                                {
                                    var messageLength = _frameBuffer[1];
                                    var message = Encoding.UTF8.GetString(_frameBuffer, 2, messageLength);

                                    if (MessageReceived != null)
                                    {
                                        MessageReceived(this, new MessageEventArgs(message));
                                    }
                                }
                                else if (_frameIndex > 2 && _frameBuffer[0] == (byte)FunctionId.Response)
                                {
                                    var senderSequence = _frameBuffer[1];
                                    var parameterLength = _frameBuffer[2];

                                    var parameter = new byte[parameterLength];

                                    Array.Copy(_frameBuffer, 3, parameter, 0, parameterLength);

                                    if (ResponseReceived != null)
                                    {
                                        ResponseReceived(this, new ResponseReceivedEventArgs(senderSequence, parameter));
                                    }
                                }
                                else
                                {
                                    if (FrameReceived != null)
                                    {
                                        var frame = new byte[_frameIndex];
                                        Array.Copy(_frameBuffer, 0, frame, 0, _frameIndex);
                                        FrameReceived(this, new FrameReceivedEventArgs(_sequence, frame));
                                    }
                                }
                            }
                            else
                            {
                                if (ErrorReceived != null)
                                {
                                    ErrorReceived(this, new MessageEventArgs("CRC Mismatch"));
                                }

                                _state = ReadState.WaitingForRecovery;
                            }

                            _state = ReadState.WaitingForSequence;
                            break;
                    }
                }
            }
        }

        private void WriteEscaped(byte b)
        {
            switch (b)
            {
                case (byte)Slip.End:
                    Write((byte)Slip.Escape);
                    Write((byte)Slip.EscapedEnd);
                    break;
                case (byte)Slip.Escape:
                    Write((byte)Slip.Escape);
                    Write((byte)Slip.EscapedEscape);
                    break;
                default:
                    Write(b);
                    break;
            }

            _writeChecksum ^= b;
        }

        private void Write(byte b)
        {
            _port.Write(new[] { b }, 0, 1);
        }

        private byte Read()
        {
            return (byte)_port.ReadByte();
        }

        private bool Available()
        {
            return _port.BytesToRead > 0;
        }

        private bool ReadUnescaped(out byte b)
        {
            b = Read();

            if (_escaped)
            {
                switch (b)
                {
                    case (byte)Slip.EscapedEnd:
                        b = (byte)Slip.End;
                        break;
                    case (byte)Slip.EscapedEscape:
                        b = (byte)Slip.Escape;
                        break;
                    default:
                        if (ErrorReceived != null)
                        {
                            ErrorReceived(this, new MessageEventArgs("Unexpected Escape"));
                        }

                        _state = ReadState.WaitingForRecovery;
                        break;
                }

                _escaped = false;
                _readChecksum ^= b;
            }
            else
            {
                if (b == (byte)Slip.Escape)
                {
                    _escaped = true;
                    return false; // read escaped value on next pass
                }

                if (b == (byte)Slip.End)
                {
                    switch (_state)
                    {
                        case ReadState.WaitingForRecovery:
                            _readSequence = null;
                            _state = ReadState.WaitingForSequence;
                            break;
                        case ReadState.WaitingForBytecode:
                            _state = ReadState.ProcessPayload;
                            break;
                        default:
                            if (ErrorReceived != null)
                            {
                                ErrorReceived(this, new MessageEventArgs("Unexpected End"));
                            }

                            _state = ReadState.WaitingForRecovery;
                            break;
                    }
                }
                else
                {
                    _readChecksum ^= b;
                }
            }

            return true;
        }
    }
}