//------------------------------------------------------------------------------
//  <copyright file="MessageEventArgs.cs" company="Microsoft Corporation">
//      Copyright (C) Microsoft Corporation.  All rights reserved.
//  </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Robotics.Tests.Reflecta
{
    using System;

    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; set; }
    }
}