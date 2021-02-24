using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    [ComImport]
    [Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface ICompositorInterop
    {
        ICompositionSurface CreateCompositionSurfaceForHandle(
            IntPtr swapChain);

        ICompositionSurface CreateCompositionSurfaceForSwapChain(
            IntPtr swapChain);

        CompositionGraphicsDevice CreateGraphicsDevice(
            IntPtr renderingDevice);
    }
}