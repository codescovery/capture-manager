using System;
using System.Runtime.InteropServices;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    [ComImport]
    [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface ICompositorDesktopInterop
    {
        Windows.UI.Composition.Desktop.DesktopWindowTarget CreateDesktopWindowTarget(
            IntPtr hwnd,
            bool isTopmost);
    }

}