using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace EarsDrop.Services;
public interface IDialogService { Task<bool> ConfirmDownloadAsync(string url); }
public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmDownloadAsync(string url)
    {
        var dialog = new Window { Title = "Clipboard link detected", Width = 440, Height = 190, CanResize = false };
        var result = false;
        var download = new Button { Content = "Download", IsDefault = true };
        var dismiss = new Button { Content = "Not now", IsCancel = true };
        download.Click += (_, _) => { result = true; dialog.Close(); };
        dismiss.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16, Children = { new TextBlock { Text = "Download the supported media link in your clipboard?", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, new TextBlock { Text = url, Opacity = .65, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8, Children = { dismiss, download } } } };
        var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) await dialog.ShowDialog(owner); else dialog.Show();
        return result;
    }
}
