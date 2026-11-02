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

        public delegate void MonitoredWindowFocus(IntPtr activeCapturableWindowInteropHandle);
        
        public CapturableWindowsMonitoringService(IDictionary<IntPtr, CapturableWindow> capturableWindows=null)
        {
            _capturableWindows = capturableWindows?? new Dictionary<IntPtr, CapturableWindow>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<bool> StartMonitoring()
        {
            if (_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();

            var token = _cancellationTokenSource.Token;
            FocusFirstMonitoringWindow();
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(CapturableWindowDefaultValues.RefreshRate);
                try
                {
                    PruneClosedWindows();
                    var activeCapturableWindowInteropHandle = NativeMethods.GetActiveWindow();
                    if (!IsMonitored(activeCapturableWindowInteropHandle)) continue;
                    MonitoredWindowFocused?.Invoke(_capturableWindows[activeCapturableWindowInteropHandle]);
                }
                catch (Exception ex)
                {
                    // A failure in one tick (or in a subscriber) must not kill monitoring.
                    Console.WriteLine($"Monitoring tick failed. {ex.Message}");
                }
            }
            return token.IsCancellationRequested;
        }

        private void PruneClosedWindows()
        {
            if (_capturableWindows == null || !_capturableWindows.Any()) return;
            var closedWindows = _capturableWindows.Values
                .Where(w => !NativeMethods.IsWindow(w.Handle))
                .ToList();
            foreach (var closedWindow in closedWindows)
            {
                _capturableWindows.Remove(closedWindow.Handle);
                try
                {
                    MonitoredWindowClosed?.Invoke(closedWindow);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Closed window handler failed for {closedWindow.Name}. {ex.Message}");
                }
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

        public void FocusFirstMonitoringWindow()
        {
            if (_capturableWindows != null && _capturableWindows.Any())
                MonitoredWindowFocused?.Invoke(_capturableWindows.Values.FirstOrDefault());
        }

        public CapturableWindow GetCapturableWindow(IntPtr processInteropHandle)
            => _capturableWindows[processInteropHandle];
        public event ICapturableWindowsMonitoringService.MonitoredWindowFocus MonitoredWindowFocused;
        public event ICapturableWindowsMonitoringService.MonitoredWindowClosure MonitoredWindowClosed;

        public void Dispose()
        {
            _capturableWindows?.Clear();
            MonitoredWindowFocused = null;
            MonitoredWindowClosed = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
