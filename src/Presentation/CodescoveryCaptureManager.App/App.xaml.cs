using System;
using System.Linq;
using System.Windows;
using Windows.System;
using CodescoveryCaptureManager.App.Tray;
using CodescoveryCaptureManager.App.Updates;
using CodescoveryCaptureManager.App.Windows;
using CodescoveryCaptureManager.Domain.Helpers;
using CodescoveryCaptureManager.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodescoveryCaptureManager.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private DispatcherQueueController _controller;
        private readonly Startup _startup;
        private readonly SingleInstance _singleInstance;
        private TrayIconController _tray;

        /// <summary>Set when the user (tray menu) or Windows asks to quit: closing the window then really exits.</summary>
        public bool ExitRequested { get; private set; }

        public App(SingleInstance singleInstance)
        {
            _singleInstance = singleInstance;
            _startup = new Startup(this);
            _controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
            SessionEnding += (_, _) => ExitRequested = true;
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var provider = _startup.ServiceProvider;
            var mainWindow = provider.GetRequiredService<CapturedWindows>();
            _tray = new TrayIconController(mainWindow, provider.GetRequiredService<IProfileService>(), Exit);
            _singleInstance.ListenForShowRequests(() => Dispatcher.Invoke(mainWindow.ShowFromTray));

            if (e.Args.Contains(StartupRegistration.TrayArgument)) mainWindow.StartHidden();
            else mainWindow.Show();

            _ = new UpdateService().StageUpdateAsync();
        }

        private void Exit()
        {
            ExitRequested = true;
            MainWindow?.Close();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            base.OnExit(e);
        }

        public void Dispose()
        {
            MainWindow?.Close();
        }
    }
}
