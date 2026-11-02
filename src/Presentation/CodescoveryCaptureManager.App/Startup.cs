using System;
using CodescoveryCaptureManager.App.Windows;
using CodescoveryCaptureManager.Domain.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace CodescoveryCaptureManager.App
{
    internal class Startup
    {
        private readonly IDisposable _parent;
        private readonly IServiceCollection _services;
        private IServiceProvider _serviceProvider;
        public IServiceProvider ServiceProvider => _serviceProvider ??= _services?.BuildServiceProvider();

        public Startup(IDisposable parent=null)
        {
            _parent = parent;
            _services = ConfigureServices(new ServiceCollection());
        }
        private IServiceCollection ConfigureServices(IServiceCollection services)
        {
            if(_parent!=null)
                services.AddScoped(p => _parent);

            services.AddCapturableWindowsMonitoringService();
            services.AddWindowsProcessServices();
            services.AddFocusBridge();
            services.AddTransient<CapturedWindows>();
            //_services.AddSingleton<IconTrayController>();
            return services;
        }
    }
}
