using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Interop;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Services
{
    internal class WindowsProcessServices:IWindowsProcessServices
    {

        public WindowsProcessServices()
        {
        }
        private IReadOnlyList<string> IgnoredProcess()
            => new List<string> { "applicationframehost", "shellexperiencehost", "systemsettings", "winstore.app", "searchui", "explorer" }; 
        public IReadOnlyList<CapturableWindow> GetOpenedWindows(params System.Windows.Window[] ignoredWindows)
        {
            var capturableWindows = new List<CapturableWindow>();
            NativeMethods.EnumWindows((processHandle, lParam) =>
            {
                // ignore invisible windows
                if (!NativeMethods.IsWindowVisible(processHandle))
                    return true;

                // ignore untitled windows
                var title = new StringBuilder(1024);
                NativeMethods.GetWindowText(processHandle, title, title.Capacity);
                if (string.IsNullOrWhiteSpace(title.ToString()))
                    return true;

                // ignore Window
                var ignoredWindowsList = ignoredWindows?.ToList();
                if (ignoredWindowsList != null && ignoredWindowsList.Any())
                    if (ignoredWindowsList.Any(i=> new WindowInteropHelper(i).Handle == processHandle))
                        return true;
                


                NativeMethods.GetWindowThreadProcessId(processHandle, out var processId);

                // ignore by process name
                var process = Process.GetProcessById((int)processId);
                if (IgnoredProcess().Contains(process.ProcessName.ToLower()))
                    return true;
                capturableWindows.Add(new CapturableWindow
                {
                    Handle = processHandle,
                    Name = $"{title} ({process.ProcessName}.exe)",
                    Process = process
                });

                return true;
            }, IntPtr.Zero);
            return capturableWindows;
        }

        public void Dispose()
        {
        }
    }
}
