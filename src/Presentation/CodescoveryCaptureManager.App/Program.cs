using System;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.App.Tray;
using CodescoveryCaptureManager.Domain.Diagnostics;
using Velopack;

namespace CodescoveryCaptureManager.App
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // Uninstall removes the app folder but not our "Start with Windows" entry: drop it here.
            VelopackApp.Build()
                .OnBeforeUninstallFastCallback(_ => StartupRegistration.SetEnabled(false))
                .Run();

            using var instance = SingleInstance.TryAcquire();
            if (instance == null) return;

            AppLog.Start();
            CrashReporter.Register();
            AppLog.Info($"Started. {AppInfo.DiagnosticInfo().ReplaceLineEndings("; ")}");
            try
            {
                var app = new App(instance);
                app.InitializeComponent();
                var exitCode = app.Run();
                AppLog.Info($"Exited with code {exitCode}.");
            }
            finally
            {
                AppLog.Stop();
            }
        }
    }
}
