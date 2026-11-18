using System;
using CodescoveryCaptureManager.App.Tray;
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

            var app = new App(instance);
            app.InitializeComponent();
            app.Run();
        }
    }
}
