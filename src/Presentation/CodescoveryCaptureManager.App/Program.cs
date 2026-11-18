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
            VelopackApp.Build().Run();

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
