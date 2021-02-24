using System;
using System.Collections.Generic;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Structs;
using Microsoft.Extensions.DependencyInjection;

namespace CodescoveryCaptureManager.Domain.Extensions
{
    public static class ServiceConfiguration
    {

        public static IServiceCollection AddWindowsProcessServices(this IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        => services.InjectService<IWindowsProcessServices, WindowsProcessServices>(serviceLifetime);

        public static IServiceCollection AddCapturableWindowsMonitoringService(this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            services.AddSingleton<IDictionary<IntPtr, CapturableWindow>>(c => new Dictionary<IntPtr, CapturableWindow>());
            services.InjectService<ICapturableWindowsMonitoringService, CapturableWindowsMonitoringService>(
                serviceLifetime);
            return services;
        }

        public static IServiceCollection InjectService<TService>(this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where TService : class
        => serviceLifetime switch
        {
            ServiceLifetime.Singleton => services.AddSingleton<TService>(),
            ServiceLifetime.Scoped => services.AddScoped<TService>(),
            ServiceLifetime.Transient => services.AddTransient<TService>(),
            _ => services
        };

        public static IServiceCollection InjectService<TService, TImplementation>(this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService

       => serviceLifetime switch
       {
           ServiceLifetime.Singleton => services.AddSingleton<TService, TImplementation>(),
           ServiceLifetime.Scoped => services.AddScoped<TService, TImplementation>(),
           ServiceLifetime.Transient => services.AddTransient<TService, TImplementation>(),
           _ => services
       };

        public static IServiceCollection InjectService<TService>(this IServiceCollection services, TService service,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
            where TService : class
       => serviceLifetime switch
       {
           ServiceLifetime.Singleton => services.AddSingleton<TService>(s => service),
           ServiceLifetime.Scoped => services.AddScoped<TService>(s => service),
           ServiceLifetime.Transient => services.AddTransient<TService>(s => service),
           _ => services
       };

    }
}
