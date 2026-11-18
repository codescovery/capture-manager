using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CodescoveryCaptureManager.Domain.Diagnostics;

namespace CodescoveryCaptureManager.App.Helpers
{
    /// <summary>Writes unhandled failures to the log before the process dies, so a crash leaves a trace.</summary>
    internal static class CrashReporter
    {
        public const int CrashExitCode = 1;

        private static int _reported;

        /// <summary>Handlers that do not need the WPF application: other threads and unobserved tasks.</summary>
        public static void Register()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AppLog.Fatal($"Unhandled exception (terminating: {e.IsTerminating}).", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                AppLog.Error("Unobserved task exception.", e.Exception);
                e.SetObserved();
            };
        }

        /// <summary>
        /// Records a failure on the UI thread and tells the user where the log is.
        /// Returns false when a crash was already reported (failures while shutting down are only logged).
        /// </summary>
        public static bool ReportFatal(Exception exception)
        {
            AppLog.Fatal("Unhandled exception on the UI thread.", exception);
            if (Interlocked.Exchange(ref _reported, 1) == 1) return false;

            MessageBox.Show(
                $"{AppInfo.Name} hit an unexpected error and has to close.{Environment.NewLine}{Environment.NewLine}" +
                $"{exception.Message}{Environment.NewLine}{Environment.NewLine}Details were saved to:{Environment.NewLine}{AppLog.FilePath}",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }
}
