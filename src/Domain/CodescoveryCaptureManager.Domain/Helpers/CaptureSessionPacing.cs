using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinRT;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    /// <summary>
    /// Windows Graphics Capture paces frames conservatively by default (measured: one frame every third refresh on a
    /// 164 Hz display). <c>GraphicsCaptureSession.MinUpdateInterval</c> (Windows 11 24H2, build 26100) lifts that.
    /// The property is not in the 19041 projection this project targets, so it is set through the ABI interface.
    /// </summary>
    internal static class CaptureSessionPacing
    {
        private const int PutMinUpdateIntervalSlot = 7;

        private static readonly Guid GraphicsCaptureSession5 = new("67c0ea62-1f85-5061-925a-239be0ac09cb");

        /// <summary>Returns false on Windows versions without the property (capture then keeps the default pacing).</summary>
        public static unsafe bool TrySetMinUpdateInterval(GraphicsCaptureSession session, TimeSpan interval)
        {
            var inspectable = MarshalInspectable<object>.FromManaged(session);
            try
            {
                if (Marshal.QueryInterface(inspectable, in GraphicsCaptureSession5, out var session5) < 0) return false;
                try
                {
                    var vtable = *(IntPtr**)session5;
                    var putMinUpdateInterval = (delegate* unmanaged[Stdcall]<IntPtr, long, int>)vtable[PutMinUpdateIntervalSlot];
                    return putMinUpdateInterval(session5, interval.Ticks) >= 0;
                }
                finally
                {
                    Marshal.Release(session5);
                }
            }
            finally
            {
                Marshal.Release(inspectable);
            }
        }
    }
}
