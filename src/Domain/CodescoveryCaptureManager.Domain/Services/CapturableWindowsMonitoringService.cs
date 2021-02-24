using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            FocusFirstMonitoringWindow();
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(100);
                var activeCapturableWindowInteropHandle = NativeMethods.GetActiveWindow();
                if(_capturableWindows==null) continue;
                if (!_capturableWindows.Any() ) continue;
                if (!_capturableWindows.ContainsKey(activeCapturableWindowInteropHandle)) continue;
                if (!IsMonitored(activeCapturableWindowInteropHandle)) continue;
                MonitoredWindowFocused?.Invoke(_capturableWindows[activeCapturableWindowInteropHandle]);
            }
            return _cancellationTokenSource.IsCancellationRequested;
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

        public void Dispose()
        {
            _capturableWindows?.Clear();
            MonitoredWindowFocused = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
