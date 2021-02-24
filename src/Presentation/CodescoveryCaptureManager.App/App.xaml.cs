using System;
using System.Windows;
using Windows.System;
using CodescoveryCaptureManager.App.Windows;
using CodescoveryCaptureManager.Domain.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace CodescoveryCaptureManager.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    
    {
        //private readonly IconTrayController _iconTrayController;
        private DispatcherQueueController _controller;
        private readonly Startup _startup;
        public App()
        {
            
            _startup = new Startup(this);
            _controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
            //_iconTrayController = ServiceProvider?.GetRequiredService<IconTrayController>();
        }


        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            
            var mainWindow = _startup.ServiceProvider.GetService<CapturedWindows>();
            //if (OperatingSystemHelper.IsWindows())
            //    _iconTrayController.Configure(mainWindow);
            mainWindow?.Show();
        }
        public void Dispose()
        {
            MainWindow?.Close();
        }
    }
}
