using System;
using System.IO;
using CodescoveryCaptureManager.Domain.Services;

namespace CodescoveryCaptureManager.Domain.Diagnostics
{
    /// <summary>
    /// Process-wide log. Entries are dropped until <see cref="Start"/> is called (tests and tools never write files).
    /// Privacy: log process names and window handles, never window titles.
    /// </summary>
    public static class AppLog
    {
        private static volatile RollingFileLog _log;

        /// <summary>Folder of the log files, next to profiles and settings.</summary>
        public static string DefaultDirectory => Path.Combine(JsonProfileStore.DefaultDirectory, "logs");

        public static string FilePath => _log?.FilePath ?? Path.Combine(DefaultDirectory, RollingFileLog.FileName);

        public static void Start(string directory = null)
        {
            var previous = _log;
            _log = new RollingFileLog(directory ?? DefaultDirectory);
            previous?.Dispose();
        }

        public static void Stop()
        {
            var current = _log;
            _log = null;
            current?.Dispose();
        }

        public static void Info(string message) => _log?.Write(LogLevel.Info, message);

        public static void Warning(string message, Exception exception = null) => _log?.Write(LogLevel.Warning, message, exception);

        public static void Error(string message, Exception exception = null) => _log?.Write(LogLevel.Error, message, exception);

        public static void Fatal(string message, Exception exception = null) => _log?.Write(LogLevel.Fatal, message, exception);
    }
}
