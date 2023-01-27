using System;
using System.Windows;
using System.Windows.Interop;

namespace CodescoveryCaptureManager.Domain.Extensions
{
    public static class WindowExtensions
    {
        public static IntPtr GetHandler(this Window window) => new WindowInteropHelper(window).Handle;

    }
}