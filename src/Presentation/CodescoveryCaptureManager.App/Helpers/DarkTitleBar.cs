using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CodescoveryCaptureManager.App.Helpers
{
    internal static class DarkTitleBar
    {
        private const int UseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Asks DWM for a dark title bar. Call from <c>OnSourceInitialized</c>.</summary>
        public static void Apply(Window window)
        {
            var enabled = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
        }
    }
}
