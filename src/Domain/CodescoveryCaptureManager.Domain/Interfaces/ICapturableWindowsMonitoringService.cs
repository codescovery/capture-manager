using System;
using System.Threading.Tasks;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface ICapturableWindowsMonitoringService:IDisposable
    {
        Task<bool> StartMonitoring();
        bool IsMonitoring();
        void StopMonitoring();
        void AddMonitoringWindow(CapturableWindow capturableWindow);
        void RemoveMonitoringWindow(IntPtr processInteropHandle);
        bool IsMonitored(IntPtr processInteropHandle);
        bool HasMonitoredWindows();
        System.Collections.Generic.IReadOnlyList<CapturableWindow> GetMonitoredWindows();
        /// <summary>True while a window of another app, not monitored, has focus.</summary>
        bool IsBlurred { get; }
        /// <summary>Raises <see cref="MonitoredWindowFocused"/> only if a monitored window is the foreground window.</summary>
        void SyncWithForegroundWindow();
        CapturableWindow GetCapturableWindow(IntPtr processInteropHandle);
        
        delegate void MonitoredWindowFocus(CapturableWindow activeCapturableWindow);
        event MonitoredWindowFocus MonitoredWindowFocused;

        delegate void MonitoredWindowsBlur();
        /// <summary>Raised once when focus moves to a window outside the app that is not monitored.</summary>
        event MonitoredWindowsBlur MonitoredWindowsBlurred;

        delegate void MonitoredWindowClosure(CapturableWindow closedCapturableWindow);
        event MonitoredWindowClosure MonitoredWindowClosed;
    }
}