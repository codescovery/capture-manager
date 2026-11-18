using System;
using Microsoft.Win32;

namespace CodescoveryCaptureManager.App.Tray
{
    /// <summary>"Start with Windows" through the per-user Run key. Starts hidden in the tray.</summary>
    internal static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CodescoveryCaptureManager";
        public const string TrayArgument = "--tray";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) != null;
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;
            if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {TrayArgument}");
            else key.DeleteValue(ValueName, false);
        }
    }
}
