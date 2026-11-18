using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodescoveryCaptureManager.Domain.Constants;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Interop;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Services
{
    internal class CapturableWindowsMonitoringService:ICapturableWindowsMonitoringService
    {
        private readonly IDictionary<IntPtr, CapturableWindow> _capturableWindows;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isBlurred;

        public delegate void MonitoredWindowFocus(IntPtr activeCapturableWindowInteropHandle);

        public CapturableWindowsMonitoringService(IDictionary<IntPtr, CapturableWindow> capturableWindows=null)
        {
            _capturableWindows = capturableWindows?? new Dictionary<IntPtr, CapturableWindow>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // Focus changes arrive through the WinEvent hook; the slow loop only catches what the hook cannot see
        // (a window closed in the background, a title that changed, a missed event).
        public async Task<bool> StartMonitoring()
        {
            if (_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();

            var token = _cancellationTokenSource.Token;
            using var watcher = new ForegroundWindowWatcher(CheckForegroundWindow);
            if (!watcher.IsActive)
                Console.WriteLine("Foreground hook not installed; focus is checked once per interval only.");

            SyncWithForegroundWindow();
            while (!token.IsCancellationRequested)
            {
                await WaitForNextCheck(token);
                CheckForegroundWindow();
            }
            return token.IsCancellationRequested;
        }

        private static async Task WaitForNextCheck(CancellationToken token)
        {
            try
            {
                await Task.Delay(CapturableWindowDefaultValues.SafetyCheckInterval, token);
            }
            catch (OperationCanceledException)
            {
                // Stopping: the caller's loop condition ends monitoring.
            }
        }

        private void CheckForegroundWindow()
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            try
            {
                PruneClosedWindows();
                EvaluateForeground(NativeMethods.GetActiveWindow());
            }
            catch (Exception ex)
            {
                // A failure in one check (or in a subscriber) must not kill monitoring.
                Console.WriteLine($"Foreground check failed. {ex.Message}");
            }
        }

        /// <summary>Decides what the given foreground window means: a monitored window got focus, or focus left them.</summary>
        internal void EvaluateForeground(IntPtr foregroundHandle)
        {
            if (IsForeignWindow(foregroundHandle))
                ForegroundWindowChanging?.Invoke(foregroundHandle);

            if (!IsMonitored(foregroundHandle))
            {
                RaiseBlurredOnce(foregroundHandle);
                return;
            }
            _isBlurred = false;
            MonitoredWindowFocused?.Invoke(_capturableWindows[foregroundHandle]);
        }

        private void PruneClosedWindows()
        {
            if (_capturableWindows == null || !_capturableWindows.Any()) return;
            var closedWindows = _capturableWindows.Values
                .Where(w => !NativeMethods.IsWindow(w.Handle))
                .ToList();
            foreach (var closedWindow in closedWindows)
                ReleaseMonitoringWindow(closedWindow.Handle);
        }

        public void ReleaseMonitoringWindow(IntPtr processInteropHandle)
        {
            if (!IsMonitored(processInteropHandle)) return;

            var releasedWindow = _capturableWindows[processInteropHandle];
            _capturableWindows.Remove(processInteropHandle);
            try
            {
                MonitoredWindowClosed?.Invoke(releasedWindow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Closed window handler failed for {releasedWindow.Name}. {ex.Message}");
            }
        }

        public bool IsMonitoring() =>
            !_cancellationTokenSource?.IsCancellationRequested ?? false;

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void AddMonitoringWindow(CapturableWindow capturableWindow)
        {
            if (!IsMonitored(capturableWindow.Handle))
                _capturableWindows.Add(capturableWindow.Handle,capturableWindow);
        }

        public void RemoveMonitoringWindow(IntPtr processInteropHandle)
        {
            if (IsMonitored(processInteropHandle))
                _capturableWindows.Remove(processInteropHandle);
        }


        public bool IsMonitored(IntPtr processInteropHandle) => _capturableWindows != null &&
                                                                _capturableWindows.Any() &&
                                                                _capturableWindows.ContainsKey(processInteropHandle);

        public bool HasMonitoredWindows() => _capturableWindows != null && _capturableWindows.Any();

        public IReadOnlyList<CapturableWindow> GetMonitoredWindows()
            => _capturableWindows?.Values.ToList() ?? new List<CapturableWindow>();

        public bool IsBlurred => _isBlurred;

        // A window of this app (picker, capture window) is neutral: the last view stays. Anything else unpicked blanks the view.
        private static bool IsForeignWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
            return processId != Environment.ProcessId;
        }

        private void RaiseBlurredOnce(IntPtr foregroundHandle)
        {
            if (_isBlurred || !IsForeignWindow(foregroundHandle)) return;
            _isBlurred = true;
            MonitoredWindowsBlurred?.Invoke();
        }

        // Re-evaluates focus right now. A monitored window is shown only if it really is the foreground window;
        // otherwise nothing is raised (never show an unfocused window).
        public void SyncWithForegroundWindow()
        {
            var foregroundHandle = NativeMethods.GetActiveWindow();
            if (!IsMonitored(foregroundHandle)) return;

            _isBlurred = false;
            MonitoredWindowFocused?.Invoke(_capturableWindows[foregroundHandle]);
        }

        public CapturableWindow GetCapturableWindow(IntPtr processInteropHandle)
            => _capturableWindows[processInteropHandle];
        public event ICapturableWindowsMonitoringService.MonitoredWindowFocus MonitoredWindowFocused;
        public event ICapturableWindowsMonitoringService.MonitoredWindowClosure MonitoredWindowClosed;
        public event ICapturableWindowsMonitoringService.MonitoredWindowsBlur MonitoredWindowsBlurred;
        public event Action<IntPtr> ForegroundWindowChanging;

        public void Dispose()
        {
            _capturableWindows?.Clear();
            MonitoredWindowFocused = null;
            MonitoredWindowClosed = null;
            MonitoredWindowsBlurred = null;
            ForegroundWindowChanging = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
