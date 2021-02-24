using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Events
{
    public class MonitoredWindowsEvents
    {
        public event ICapturableWindowsMonitoringService.MonitoredWindowFocus MonitoredWindowFocused;
    }
}
