using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Capture;
using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Helpers
{
public  static class CaptureHelper
{
    static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    public static GraphicsCaptureItem CreateItemForWindow(IntPtr windowInteropHandler)
    {
        var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
        var interop = (IGraphicsCaptureItemInterop) factory;
        var itemPointer = interop.CreateForWindow(windowInteropHandler, GraphicsCaptureItemGuid);
        var item = Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
        Marshal.Release(itemPointer);

        return item;
    }
    public static void SetWindow(this GraphicsCapturePicker picker, IntPtr hwnd)
    {
        var interop = (IInitializeWithWindow)(object)picker;
        interop.Initialize(hwnd);
    }
}
}
