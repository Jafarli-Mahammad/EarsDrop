using System.Collections.ObjectModel;
using Application.UseCases.DownloadMedia;
using Application.UseCases.RetryDownload;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;
using EarsDrop.Services;
using MediatR;

namespace EarsDrop.ViewModels;
public partial class QueueViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    private readonly IClipboardMonitor _clipboard;
    private readonly IDialogService _dialogs;

    // Tracks the last URL queued by the clipboard monitor to prevent double-firing
    // when ClipboardDetected and PasteAsync both read the same clipboard content.
    private string _lastQueuedUrl = string.Empty;

    public ObservableCollection<DownloadCardViewModel> Downloads { get; } = [];
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private OutputFormat selectedFormat = OutputFormat.Mp3;
    [ObservableProperty] private bool autoDownload;
    [ObservableProperty] private bool isDownloading;
    [ObservableProperty] private string feedback = "Paste a supported URL to start.";
    public Array Formats => Enum.GetValues<OutputFormat>();

    public QueueViewModel(
        ISender sender,
        INotificationService notifications,
        IClipboardMonitor clipboard,
        IDialogService dialogs)
    {
        _sender = sender;
        _notifications = notifications;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _clipboard.UrlDetected += ClipboardDetected;
        _clipboard.Start();
    }
    private async void ClipboardDetected(object? sender, string detectedUrl)
    {
        // Ignore if we already queued this exact URL (prevents double-fire
        // when the clipboard stays the same but the event re-fires).
        if (detectedUrl == _lastQueuedUrl) return;

        _notifications.ShowNotification("Link detected", "A supported media link is ready to download.");

        if (AutoDownload)
        {
            Url = detectedUrl;
            await TriggerDownloadAsync(detectedUrl);
        }
        else if (await _dialogs.ConfirmDownloadAsync(detectedUrl))
        {
            Url = detectedUrl;
            await TriggerDownloadAsync(detectedUrl);
        }
    }
    [RelayCommand] private async Task PasteAsync()
    {
        var window = (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is not null)
            Url = await clipboard.TryGetTextAsync() ?? string.Empty;
    }
    [RelayCommand] public async Task StartDownloadAsync()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out _)) { Feedback = "Enter a valid supported URL."; return; }
        await TriggerDownloadAsync(Url);
    }

    private async Task TriggerDownloadAsync(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out _)) { Feedback = "Enter a valid supported URL."; return; }

        // Deduplicate: if we are already downloading this exact URL, skip.
        if (targetUrl == _lastQueuedUrl && IsDownloading) return;

        _lastQueuedUrl = targetUrl;
        IsDownloading = true;

        var card = new DownloadCardViewModel
        {
            Title = targetUrl,
            Status = "Downloading",
            DownloadStatus = DownloadStatus.Downloading,
            Progress = 5,
            Speed = "Starting…"
        };
        Downloads.Insert(0, card);
        Feedback = "Download started.";
        Url = string.Empty; // Clear URL field so a paste of the same URL later is treated as new

        try
        {
            var result = await _sender.Send(new DownloadMediaCommand(targetUrl, SelectedFormat));
            if (result.IsSuccess)
            {
                var completed = DownloadCardViewModel.FromDto(result.Value);
                var index = Downloads.IndexOf(card);
                if (index >= 0) Downloads[index] = completed;
                Feedback = "Download completed.";
                _notifications.ShowNotification("Download complete", completed.Title, completed.OutputPath);
            }
            else
            {
                card.Status = result.Error.Message;
                card.DownloadStatus = DownloadStatus.Failed;
                card.Speed = "Failed";
                Feedback = result.Error.Message;
                _notifications.ShowNotification("Download failed", result.Error.Message);
            }
        }
        catch (Exception ex)
        {
            card.Status = ex.Message;
            card.DownloadStatus = DownloadStatus.Failed;
            card.Speed = "Failed";
            Feedback = ex.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }
    [RelayCommand] private async Task RetryAsync(DownloadCardViewModel card)
    { var result = await _sender.Send(new RetryDownloadCommand(card.Id)); if (result.IsSuccess) { var i = Downloads.IndexOf(card); Downloads[i] = DownloadCardViewModel.FromDto(result.Value); } }
    [RelayCommand] private void OpenFolder(DownloadCardViewModel card) { if (!string.IsNullOrWhiteSpace(card.OutputPath)) _notifications.OpenFileLocation(card.OutputPath); }
    partial void OnAutoDownloadChanged(bool value) { }
}
