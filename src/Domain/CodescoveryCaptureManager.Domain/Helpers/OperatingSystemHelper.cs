using System;
using CodescoveryCaptureManager.Domain.Enums;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    public static class OperatingSystemHelper
    {
        private static readonly PlatformID Platform = Environment.OSVersion.Platform;
        public static bool IsWindows()
            => Platform == PlatformID.Win32Windows || Platform == PlatformID.Win32NT ||
               Platform == PlatformID.Win32S || Platform == PlatformID.WinCE;


        public static bool IsMac()
            => Platform == PlatformID.MacOSX;


        public static bool IsUnix()
            => Platform == PlatformID.Unix;

        public static OperatingSystemType GetOperatingSystem()
        {
            if (IsWindows())
                return OperatingSystemType.Windows;
            if (IsUnix())
                return OperatingSystemType.Unix;

            return IsMac() ? OperatingSystemType.Mac : OperatingSystemType.None;
        }

    }
}