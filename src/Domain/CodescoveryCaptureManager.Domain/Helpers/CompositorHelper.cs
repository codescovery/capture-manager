using System;
using Windows.UI.Composition;
using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public static class CompositorHelper
    {
        public static CompositionTarget CreateDesktopWindowTarget(this Compositor compositor, IntPtr hwnd, bool isTopmost)
        {
            var desktopInterop = WinRtInteropHelper.AsComInterface<ICompositorDesktopInterop>(compositor);
            var targetPointer = desktopInterop.CreateDesktopWindowTarget(hwnd, isTopmost);
            return WinRtInteropHelper.FromPointer<CompositionTarget>(targetPointer);
        }

        public static ICompositionSurface CreateCompositionSurfaceForSwapChain(this Compositor compositor, SharpDX.DXGI.SwapChain1 swapChain)
        {
            var interop = WinRtInteropHelper.AsComInterface<ICompositorInterop>(compositor);
            var surfacePointer = interop.CreateCompositionSurfaceForSwapChain(swapChain.NativePointer);
            return WinRtInteropHelper.FromPointer<ICompositionSurface>(surfacePointer);
        }
    }
}
