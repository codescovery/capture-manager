using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using CodescoveryCaptureManager.Domain.Interfaces;
using WinRT;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public static class CaptureHelper
    {
        private static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        public static GraphicsCaptureItem CreateItemForWindow(IntPtr windowInteropHandler)
        {
            var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
            var itemPointer = interop.CreateForWindow(windowInteropHandler, GraphicsCaptureItemGuid);
            return WinRtInteropHelper.FromPointer<GraphicsCaptureItem>(itemPointer);
        }

        public static void SetWindow(this GraphicsCapturePicker picker, IntPtr hwnd)
        {
            var interop = WinRtInteropHelper.AsComInterface<IInitializeWithWindow>(picker);
            interop.Initialize(hwnd);
        }
    }
}
