using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using Application.UseCases.DownloadMedia;
using Application.UseCases.RetryDownload;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;
using EarsDrop.Services;
using MediatR;

namespace EarsDrop.ViewModels;
public partial class QueueViewModel : ViewModelBase
{
    private sealed record DownloadRequest(string Url, OutputFormat Format);

    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    private readonly IClipboardMonitor _clipboard;
    private readonly IDialogService _dialogs;
    private readonly Channel<DownloadRequest> _downloadQueue = Channel.CreateUnbounded<DownloadRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly object _queueGate = new();
    private readonly Dictionary<string, DateTimeOffset> _recentUrls = new(StringComparer.Ordinal);
    private readonly Task _queueWorker;

    public int DuplicateLinkCooldownSeconds { get; set; } = 2;
    public bool EnableMetadataEnrichment { get; set; }
    public bool EnableCoverArtEmbedding { get; set; } = true;
    public bool EnableTagWriting { get; set; } = true;

    private int _queuedDownloads;

    private TimeSpan DuplicateLinkCooldown => TimeSpan.FromSeconds(Math.Max(0, DuplicateLinkCooldownSeconds));

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
        _queueWorker = ProcessQueueAsync();
    }
    private async void ClipboardDetected(object? sender, string detectedUrl)
    {
        // 'async void' event handler: any unhandled exception here would crash the
        // whole application, so guard the entire body.
        try
        {
            if (AutoDownload)
            {
                QueueDownload(detectedUrl, SelectedFormat);
            }
            else if (await _dialogs.ConfirmDownloadAsync(detectedUrl))
            {
                QueueDownload(detectedUrl, SelectedFormat);
            }
        }
        catch (Exception ex)
        {
            Feedback = $"Could not handle detected link: {ex.Message}";
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
    [RelayCommand] public Task StartDownloadAsync()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out _)) { Feedback = "Enter a valid supported URL."; return Task.CompletedTask; }
        QueueDownload(Url, SelectedFormat);
        Url = string.Empty;
        return Task.CompletedTask;
    }

    private void QueueDownload(string targetUrl, OutputFormat format)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
        {
            Feedback = "Enter a valid supported URL.";
            return;
        }

        lock (_queueGate)
        {
            var now = DateTimeOffset.UtcNow;
            var cutoff = now - DuplicateLinkCooldown;

            foreach (var entry in _recentUrls.Where(entry => entry.Value < cutoff).ToArray())
            {
                _recentUrls.Remove(entry.Key);
            }

            if (_recentUrls.TryGetValue(targetUrl, out var lastSeen) && now - lastSeen < DuplicateLinkCooldown)
            {
                return;
            }

            _recentUrls[targetUrl] = now;

            if (!_downloadQueue.Writer.TryWrite(new DownloadRequest(targetUrl, format)))
            {
                _recentUrls.Remove(targetUrl);
                return;
            }

            _queuedDownloads++;
            IsDownloading = true;
            Feedback = "Download queued.";
            Url = targetUrl;
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var request in _downloadQueue.Reader.ReadAllAsync())
            {
                await TriggerDownloadAsync(request);
                lock (_queueGate)
                {
                    _queuedDownloads = Math.Max(0, _queuedDownloads - 1);
                    IsDownloading = _queuedDownloads > 0;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Feedback = $"Queue stopped unexpectedly: {ex.Message}");
        }
    }

    private async Task TriggerDownloadAsync(DownloadRequest request)
    {
        var card = new DownloadCardViewModel
        {
            Title = request.Url,
            Status = "Downloading",
            DownloadStatus = DownloadStatus.Downloading,
            Progress = 5,
            Speed = "Starting…"
        };

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Downloads.Insert(0, card);
            Feedback = "Download started.";
        });

        try
        {
            var result = await _sender.Send(new DownloadMediaCommand(
                request.Url,
                request.Format,
                EnableMetadataEnrichment,
                EnableCoverArtEmbedding,
                EnableTagWriting));
            if (result.IsSuccess)
            {
                var completed = DownloadCardViewModel.FromDto(result.Value);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var index = Downloads.IndexOf(card);
                    if (index >= 0) Downloads[index] = completed;
                    Feedback = "Download completed.";
                });
                _notifications.ShowNotification("Download complete", completed.Title, completed.OutputPath);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    card.Status = result.Error.Message;
                    card.DownloadStatus = DownloadStatus.Failed;
                    card.Speed = "Failed";
                    Feedback = result.Error.Message;
                });
                _notifications.ShowNotification("Download failed", result.Error.Message);
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                card.Status = ex.Message;
                card.DownloadStatus = DownloadStatus.Failed;
                card.Speed = "Failed";
                Feedback = ex.Message;
            });
        }
    }
    [RelayCommand] private async Task RetryAsync(DownloadCardViewModel card)
    { var result = await _sender.Send(new RetryDownloadCommand(card.Id)); if (result.IsSuccess) { var i = Downloads.IndexOf(card); if (i >= 0) Downloads[i] = DownloadCardViewModel.FromDto(result.Value); } }
    [RelayCommand] private void OpenFolder(DownloadCardViewModel card) { if (!string.IsNullOrWhiteSpace(card.OutputPath)) _notifications.OpenFileLocation(card.OutputPath); }
    partial void OnAutoDownloadChanged(bool value) { }
}
