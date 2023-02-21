using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
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
            var capturableWindows = CreateNewListOfCapturableWindows();
            NativeMethods.EnumWindows((processHandle, lParam) =>
            {
                if (!IsWindowVisible(processHandle))
                    return true;

                var title = CreateNewTitleStringBuilder();
                GetWindowText(processHandle, title);
                if (IgnoreItemBecauseTitleIsNull(title))
                    return true;

                if (IsProcessInIgnoredList(ignoredWindows, processHandle)) return true;
                
                var processId = GetWindowThreadProcessId(processHandle);
                if (IgnoreProcessByName(processId, out var process)) return true;
                AddProcessToCapturableWindowsList(capturableWindows, processHandle, title, process);

                return true;
            }, IntPtr.Zero);
            return capturableWindows;
        }

        private static List<CapturableWindow> CreateNewListOfCapturableWindows()
        {
            return new List<CapturableWindow>();
        }

        private static StringBuilder CreateNewTitleStringBuilder()
        {
            return new StringBuilder(1024);
        }

        private static bool IsWindowVisible(IntPtr processHandle)
        {
            return NativeMethods.IsWindowVisible(processHandle);
        }

        private static bool IgnoreItemBecauseTitleIsNull(StringBuilder title)
        {
            return string.IsNullOrWhiteSpace(title.ToString());
        }

        private static void GetWindowText(IntPtr processHandle, StringBuilder title)
        {
            NativeMethods.GetWindowText(processHandle, title, title.Capacity);
        }

        private static uint GetWindowThreadProcessId(IntPtr processHandle)
        {
            NativeMethods.GetWindowThreadProcessId(processHandle, out var processId);
            return processId;
        }

        private static void AddProcessToCapturableWindowsList(List<CapturableWindow> capturableWindows, IntPtr processHandle, StringBuilder title,
            Process process)
        {
            capturableWindows.Add(new CapturableWindow
            {
                Handle = processHandle,
                Name = $"{title} ({process.ProcessName}.exe)",
                Process = process
            });
        }

        private static bool IsProcessInIgnoredList(Window[] ignoredWindows, IntPtr processHandle)
        {
            var ignoredWindowsList = ignoredWindows?.ToList();
            if (ignoredWindowsList == null || !ignoredWindowsList.Any()) return false;
            return ignoredWindowsList.Any(i => new WindowInteropHelper(i).Handle == processHandle);
        }

        private bool IgnoreProcessByName(uint processId, out Process process)
        {
            process = Process.GetProcessById((int) processId);
            if (IgnoredProcess().Contains(process.ProcessName.ToLower()))
                return true;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
