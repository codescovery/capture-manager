using System;
using System.Runtime.InteropServices;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    [ComImport]
    [Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface ICompositorInterop
    {
        IntPtr CreateCompositionSurfaceForHandle(IntPtr swapChain);

        IntPtr CreateCompositionSurfaceForSwapChain(IntPtr swapChain);

        IntPtr CreateGraphicsDevice(IntPtr renderingDevice);
    }
}
