using System;
using System.Runtime.InteropServices;
using WinRT;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    internal static class WinRtInteropHelper
    {
        public static TComInterface AsComInterface<TComInterface>(object winRtObject) where TComInterface : class
        {
            var pointer = MarshalInspectable<object>.FromManaged(winRtObject);
            try
            {
                return (TComInterface)Marshal.GetObjectForIUnknown(pointer);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        public static TWinRt FromPointer<TWinRt>(IntPtr abiPointer)
        {
            try
            {
                return MarshalInspectable<TWinRt>.FromAbi(abiPointer);
            }
            finally
            {
                Marshal.Release(abiPointer);
            }
        }
    }
}
