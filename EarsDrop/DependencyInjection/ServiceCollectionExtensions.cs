using Application;
using EarsDrop.Services;
using EarsDrop.ViewModels;
using EarsDrop.Views;
using Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EarsDrop.DependencyInjection;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEarsDropPresentation(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddLogging(builder => builder.AddConsole());
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddSingleton<IClipboardMonitor, ClipboardMonitor>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<DownloadHistoryViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
