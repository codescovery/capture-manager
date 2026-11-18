using System;
using CodescoveryCaptureManager.Domain.Interop;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>
    /// Tells when the foreground window changes, through <c>SetWinEventHook</c> instead of polling.
    /// Create and dispose on a thread with a message loop (the UI thread): Windows delivers the events there.
    /// </summary>
    internal sealed class ForegroundWindowWatcher : IDisposable
    {
        private const uint EventSystemForeground = 0x0003;
        // Restoring a minimized window does not always raise a foreground event.
        private const uint EventSystemMinimizeEnd = 0x0017;
        private const uint OutOfContext = 0x0000;

        // Kept in a field: the native side holds only a function pointer, the GC must not collect the delegate.
        private readonly NativeMethods.WinEventProc _callback;
        private readonly Action _onChanged;
        private readonly IntPtr _foregroundHook;
        private readonly IntPtr _restoreHook;

        public ForegroundWindowWatcher(Action onChanged)
        {
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _callback = OnWinEvent;
            _foregroundHook = Hook(EventSystemForeground);
            _restoreHook = Hook(EventSystemMinimizeEnd);
        }

        /// <summary>False when Windows refused the hook; the caller then relies on its periodic check only.</summary>
        public bool IsActive => _foregroundHook != IntPtr.Zero;

        private IntPtr Hook(uint eventId)
            => NativeMethods.SetWinEventHook(eventId, eventId, IntPtr.Zero, _callback, 0, 0, OutOfContext);

        private void OnWinEvent(IntPtr hook, uint eventId, IntPtr windowHandle, int objectId, int childId, uint threadId, uint time)
            => _onChanged();

        public void Dispose()
        {
            if (_foregroundHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_foregroundHook);
            if (_restoreHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_restoreHook);
        }
    }
}
