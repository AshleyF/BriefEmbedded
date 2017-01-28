using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Microsoft.Robotics.Microcontroller
{
    public static class Utility
    {
        public static int BytesAsInt(byte[] bytes)
        {
            // interpreted as 1-, 2- or 4-byte sign extended int
            var value = 0;
            switch (bytes.Length)
            {
                case 0:
                    value = 0;
                    break;
                case 1:
                    value = (sbyte)bytes[0];
                    break;
                case 2:
                    value = (short)((ushort)bytes[0] << 8 | (ushort)bytes[1]);
                    break;
                case 4:
                    value = (int)((uint)bytes[0] << 24 | (uint)bytes[1] << 16 | (uint)bytes[2] << 8 | (uint)bytes[3]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected event payload size.");
            }
            return value;
        }

        public static MethodInfo FuncInfo(Func<int, int> func)
        {
            return func.GetMethodInfo();
        }
    }
}
