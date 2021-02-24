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
        void FocusFirstMonitoringWindow();
        CapturableWindow GetCapturableWindow(IntPtr processInteropHandle);
        
        delegate void MonitoredWindowFocus(CapturableWindow activeCapturableWindow);
        event MonitoredWindowFocus MonitoredWindowFocused;
    }
}