using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace EarsDrop.Services;

public interface IClipboardMonitor
{
    event EventHandler<string>? UrlDetected;
    bool IsEnabled { get; set; }
    void Start();
    void Stop();
}

public sealed class ClipboardMonitor : IClipboardMonitor, IDisposable
{
    private static readonly Regex SupportedUrl = new(
        @"^https?://(?:www\.)?(?:youtube\.com|youtu\.be|soundcloud\.com|bandcamp\.com|vimeo\.com)/\S+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(1));
    private CancellationTokenSource? _cancellation;

    // Seeded to a non-null sentinel so the very first clipboard read never
    // auto-fires for pre-existing clipboard content.
    private string _lastText = string.Empty;

    // Tracks whether the seed pass has happened. On first tick we silently
    // record whatever is in the clipboard WITHOUT firing UrlDetected.
    private bool _seeded;

    public event EventHandler<string>? UrlDetected;
    public bool IsEnabled { get; set; } = true;

    public void Start()
    {
        if (_cancellation is not null) return;
        _cancellation = new CancellationTokenSource();
        _ = MonitorAsync(_cancellation.Token);
    }

    public void Stop()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private async Task MonitorAsync(CancellationToken token)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(token))
            {
                if (!IsEnabled) continue;

                var text = await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                            as IClassicDesktopStyleApplicationLifetime;
                        var clipboard = TopLevel.GetTopLevel(lifetime?.MainWindow)?.Clipboard;
                        if (clipboard is null)
    return null;

using var data = await clipboard.TryGetDataAsync();

return data is null
    ? null
    : await data.TryGetTextAsync();
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (string.IsNullOrWhiteSpace(text)) continue;

                var candidate = text.Trim();

                if (!_seeded)
                {
                    // First tick: silently record current clipboard to prevent
                    // firing on content that was there before the app opened.
                    _lastText = candidate;
                    _seeded = true;
                    continue;
                }

                // Only fire when the clipboard *changes* to a new URL.
                if (candidate == _lastText) continue;

                _lastText = candidate;

                if (SupportedUrl.IsMatch(candidate))
                    UrlDetected?.Invoke(this, candidate);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
