//------------------------------------------------------------------------------
//  <copyright file="FrameReceivedEventArgs.cs" company="Microsoft Corporation">
//      Copyright (C) Microsoft Corporation.  All rights reserved.
//  </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Robotics.Tests.Reflecta
{
    using System;

    public class FrameReceivedEventArgs : EventArgs
    {
        public FrameReceivedEventArgs(byte sequence, byte[] frame)
        {
            Sequence = sequence;
            Frame = frame;
        }

        public byte Sequence { get; set; }

        public byte[] Frame { get; set; }
    }
}