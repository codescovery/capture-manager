using System;
using System.Runtime.InteropServices;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr hWnd, ref Guid iid);

    }
}