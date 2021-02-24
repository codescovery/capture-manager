using System.Runtime.InteropServices;
using Windows.System;
using CodescoveryCaptureManager.Domain.Enums;
using CodescoveryCaptureManager.Domain.Interop;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public static class CoreMessagingHelper
    {
        public static DispatcherQueueController CreateDispatcherQueueControllerForCurrentThread()
        {
            var options = new DispatcherQueueOptions
            {
                DwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                ThreadType = DispatcherqueueThreadType.DqtypeThreadCurrent,
                ApartmentType = DispatcherqueueThreadApartmentType.DqtatComNone
            };

            var hr = NativeMethods.CreateDispatcherQueueController(options, out var controllerPointer);
            if (hr != 0) return null;

            var controller = Marshal.GetObjectForIUnknown(controllerPointer) as DispatcherQueueController;
            Marshal.Release(controllerPointer);

            return controller;
        }
    }
}
