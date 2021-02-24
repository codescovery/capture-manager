using System;
using Windows.UI.Composition;
using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public  static class CompositorHelper
    {
        public static CompositionTarget CreateDesktopWindowTarget(this Compositor compositor, IntPtr hwnd, bool isTopmost)
        {
            var desktopInterop = (ICompositorDesktopInterop)((object)compositor);
            return desktopInterop.CreateDesktopWindowTarget(hwnd, isTopmost);
        }

        public static ICompositionSurface CreateCompositionSurfaceForSwapChain(this Compositor compositor, SharpDX.DXGI.SwapChain1 swapChain)
        {
            var interop = (ICompositorInterop)(object)compositor;
            return interop.CreateCompositionSurfaceForSwapChain(swapChain.NativePointer);
        }
    }
}
