using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace CcmDemo
{
    internal static class Native
    {
        private const uint RenderFullContent = 2;
        private const int ExtendedFrameBounds = 9;
        private const uint NoZOrder = 0x0004;
        private const uint NoActivate = 0x0010;
        public const int HotkeyMessage = 0x0312;
        public const int PauseHotkeyId = 0x4343;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        private delegate bool EnumProc(IntPtr window, IntPtr parameter);

        /// <summary>
        /// Picture of one window taken from the window itself (PrintWindow), not from the screen: it never contains
        /// anything else that happens to be on the desktop, and works while the window is covered.
        /// </summary>
        public static Bitmap Capture(IntPtr window)
        {
            GetWindowRect(window, out var outer);
            if (DwmGetWindowAttribute(window, ExtendedFrameBounds, out var visible, Marshal.SizeOf<Rect>()) != 0) visible = outer;
            var width = Math.Max(1, outer.Right - outer.Left);
            var height = Math.Max(1, outer.Bottom - outer.Top);
            using var full = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (var graphics = Graphics.FromImage(full))
            {
                var deviceContext = graphics.GetHdc();
                PrintWindow(window, deviceContext, RenderFullContent);
                graphics.ReleaseHdc(deviceContext);
            }

            var crop = Rectangle.Intersect(
                new Rectangle(visible.Left - outer.Left, visible.Top - outer.Top, visible.Right - visible.Left, visible.Bottom - visible.Top),
                new Rectangle(0, 0, width, height));
            return full.Clone(crop, PixelFormat.Format32bppRgb);
        }

        public static IntPtr FindWindow(int processId, Func<string, string, bool> matches)
        {
            var found = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (processId != 0 && owner != processId) return true;
                if (!IsWindowVisible(window) || !matches(ReadText(window, GetWindowText), ReadText(window, GetClassName))) return true;
                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private static string ReadText(IntPtr window, Func<IntPtr, StringBuilder, int, int> read)
        {
            var text = new StringBuilder(256);
            read(window, text, text.Capacity);
            return text.ToString();
        }

        public static void Place(IntPtr window, Rectangle bounds)
            => SetWindowPos(window, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, NoZOrder | NoActivate);

        public static bool IsForeground(IntPtr window) => GetForegroundWindow() == window;

        /// <summary>Gives a window of this process the foreground even when another program has it.</summary>
        public static bool TakeForeground(IntPtr window)
        {
            if (SetForegroundWindow(window) && IsForeground(window)) return true;

            var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var ownThread = GetCurrentThreadId();
            AttachThreadInput(ownThread, foregroundThread, true);
            BringWindowToTop(window);
            SetForegroundWindow(window);
            AttachThreadInput(ownThread, foregroundThread, false);
            return IsForeground(window);
        }

        public static void PostHotkey(IntPtr window)
            => PostMessage(window, HotkeyMessage, (IntPtr)PauseHotkeyId, IntPtr.Zero);

        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect bounds);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attached);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}
