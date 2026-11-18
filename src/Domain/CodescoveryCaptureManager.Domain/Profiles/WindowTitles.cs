using System;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Profiles
{
    /// <summary>Reads process name and bare title from a <see cref="CapturableWindow"/> (Name is "title (process.exe)").</summary>
    public static class WindowTitles
    {
        public static string ProcessNameOf(CapturableWindow window)
            => window.ProcessName ?? SafeProcessName(window);

        public static string TitleOf(CapturableWindow window)
        {
            var name = window.Name ?? string.Empty;
            var suffix = $" ({ProcessNameOf(window)}.exe)";
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
        }

        private static string SafeProcessName(CapturableWindow window)
        {
            try
            {
                return window.Process?.ProcessName ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
