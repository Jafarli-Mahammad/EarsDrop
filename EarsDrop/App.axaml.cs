using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EarsDrop.DependencyInjection;
using EarsDrop.Services;
using EarsDrop.ViewModels;
using EarsDrop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EarsDrop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = new ServiceCollection().AddEarsDropPresentation().BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _services.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OpenFromTray(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { } window)
        { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
    }
    private void SettingsFromTray(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { DataContext: MainViewModel model } }) model.ShowSettingsCommand.Execute(null);
        OpenFromTray(sender, e);
    }
    private void PauseDownloads(object? sender, EventArgs e) { }
    private void ResumeDownloads(object? sender, EventArgs e) { }
    private void ExitFromTray(object? sender, EventArgs e)
    { if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(); }
}
