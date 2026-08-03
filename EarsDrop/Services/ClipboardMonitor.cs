using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input.Platform;

namespace EarsDrop.Services
{
    public interface IClipboardMonitor
    {
        event EventHandler<string> UrlDetected;
        bool IsEnabled { get; set; }
        int PollIntervalMilliseconds { get; set; }
        void Start();
        void Stop();
    }

    public sealed class ClipboardMonitor : IClipboardMonitor, IDisposable
    {
        private static readonly Regex SupportedUrl = new(
            @"^https?://(?:www\.)?(?:youtube\.com|youtu\.be|soundcloud\.com|bandcamp\.com|vimeo\.com)/\S+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const int RapidPollIntervalMilliseconds = 10;
        private const int MinimumPollIntervalMilliseconds = 10;
        private const int MaximumPollIntervalMilliseconds = 1000;

        private CancellationTokenSource? _cancellation;
        private readonly SemaphoreSlim _pollGate = new(1, 1);
        private string _lastText = string.Empty;
        private bool _seeded;
        private DateTimeOffset _rapidPollingUntil = DateTimeOffset.MinValue;
        private int _pollIntervalMilliseconds = 25;

        public event EventHandler<string>? UrlDetected;

        public bool IsEnabled { get; set; } = true;

        public int PollIntervalMilliseconds
        {
            get => _pollIntervalMilliseconds;
            set => _pollIntervalMilliseconds = Math.Clamp(value, MinimumPollIntervalMilliseconds, MaximumPollIntervalMilliseconds);
        }

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
                while (!token.IsCancellationRequested)
                {
                    var delay = GetCurrentDelay();
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, token);
                    }

                    await _pollGate.WaitAsync(token);
                    try
                    {
                        if (!IsEnabled)
                            continue;

                        var text = await ReadClipboardTextAsync();

                        if (string.IsNullOrWhiteSpace(text)) continue;

                        var candidate = text.Trim();

                        if (!_seeded)
                        {
                            // First observation: silently record current clipboard to prevent
                            // firing on content that was there before the app opened.
                            _lastText = candidate;
                            _seeded = true;
                            continue;
                        }

                        // Only fire when the clipboard *changes* to a new value.
                        if (candidate == _lastText) continue;

                        _lastText = candidate;
                        _rapidPollingUntil = DateTimeOffset.UtcNow.AddMilliseconds(250);

                        if (SupportedUrl.IsMatch(candidate))
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => UrlDetected?.Invoke(this, candidate));
                        }
                    }
                    finally
                    {
                        _pollGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private TimeSpan GetCurrentDelay()
        {
            var interval = PollIntervalMilliseconds;
            if (DateTimeOffset.UtcNow < _rapidPollingUntil)
                interval = Math.Min(interval, RapidPollIntervalMilliseconds);

            return TimeSpan.FromMilliseconds(interval);
        }

        private static async Task<string?> ReadClipboardTextAsync()
        {
            try
            {
                var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                    as IClassicDesktopStyleApplicationLifetime;
                var clipboard = TopLevel.GetTopLevel(lifetime?.MainWindow)?.Clipboard;
                if (clipboard is null)
                    return null;

                return await clipboard.TryGetTextAsync();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
            _pollGate.Dispose();
        }
    }
}