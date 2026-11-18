using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CodescoveryCaptureManager.Domain.Hotkeys;

namespace CodescoveryCaptureManager.App.Helpers
{
    /// <summary>One system-wide hotkey delivered to a window (WM_HOTKEY), so it works while another program has focus.</summary>
    internal sealed class GlobalHotkey : IDisposable
    {
        private const int HotkeyMessage = 0x0312;
        private const int HotkeyId = 0x4343;
        private const uint NoRepeat = 0x4000;

        private readonly HwndSource _source;
        private readonly Action _pressed;

        public GlobalHotkey(Window window, Action pressed)
        {
            _pressed = pressed;
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
            _source.AddHook(OnWindowMessage);
        }

        public bool IsRegistered { get; private set; }

        /// <summary>Replaces the current hotkey. False when Windows refuses it (another program owns the combination).</summary>
        public bool Register(Hotkey hotkey)
        {
            Unregister();
            IsRegistered = RegisterHotKey(_source.Handle, HotkeyId, (uint)hotkey.Modifiers | NoRepeat, (uint)hotkey.VirtualKey);
            return IsRegistered;
        }

        public void Unregister()
        {
            if (IsRegistered) UnregisterHotKey(_source.Handle, HotkeyId);
            IsRegistered = false;
        }

        private IntPtr OnWindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != HotkeyMessage || wParam.ToInt32() != HotkeyId) return IntPtr.Zero;

            handled = true;
            _pressed();
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source.RemoveHook(OnWindowMessage);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr window, int id);
    }
}
