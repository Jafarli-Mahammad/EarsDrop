using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EarsDrop.Services;

namespace EarsDrop.ViewModels;
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IClipboardMonitor _clipboard; private readonly IThemeService _theme; private readonly QueueViewModel _queue;
    [ObservableProperty] private bool startWithWindows;
    [ObservableProperty] private bool monitorClipboard = true;
    [ObservableProperty] private bool autoDownload;
    [ObservableProperty] private bool minimizeToTray = true;
    [ObservableProperty] private string downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    [ObservableProperty] private string defaultFormat = "MP3";
    [ObservableProperty] private string defaultQuality = "Best";
    [ObservableProperty] private int maximumSimultaneousDownloads = 3;
    [ObservableProperty] private bool fetchMetadata;
    [ObservableProperty] private bool embedCoverArt = true;
    [ObservableProperty] private bool writeId3Tags = true;
    [ObservableProperty] private int clipboardDuplicateCooldownSeconds = 2;
    [ObservableProperty] private int clipboardPollIntervalMilliseconds = 25;
    [ObservableProperty] private string theme = "Dark";
    [ObservableProperty] private string accentColor = "Violet";
    [ObservableProperty] private string language = "English";
    [ObservableProperty] private string ytDlpPath = "yt-dlp";
    [ObservableProperty] private string ffmpegPath = "ffmpeg";
    public string[] Formats { get; } = ["MP3", "MP4"]; public string[] Qualities { get; } = ["Best", "320 kbps", "256 kbps", "192 kbps"]; public string[] Themes { get; } = ["Dark", "Light"]; public string[] Accents { get; } = ["Violet", "Blue", "Teal", "Rose"]; public string[] Languages { get; } = ["English", "Spanish", "German", "French"]; 
    public SettingsViewModel(IClipboardMonitor clipboard, IThemeService theme, QueueViewModel queue) { _clipboard = clipboard; _theme = theme; _queue = queue; _queue.DuplicateLinkCooldownSeconds = ClipboardDuplicateCooldownSeconds; _queue.EnableMetadataEnrichment = FetchMetadata; _queue.EnableCoverArtEmbedding = EmbedCoverArt; _queue.EnableTagWriting = WriteId3Tags; _clipboard.PollIntervalMilliseconds = ClipboardPollIntervalMilliseconds; }
    partial void OnMonitorClipboardChanged(bool value) => _clipboard.IsEnabled = value;
    partial void OnAutoDownloadChanged(bool value) => _queue.AutoDownload = value;
    partial void OnFetchMetadataChanged(bool value) => _queue.EnableMetadataEnrichment = value;
    partial void OnEmbedCoverArtChanged(bool value) => _queue.EnableCoverArtEmbedding = value;
    partial void OnWriteId3TagsChanged(bool value) => _queue.EnableTagWriting = value;
    partial void OnClipboardDuplicateCooldownSecondsChanged(int value) => _queue.DuplicateLinkCooldownSeconds = Math.Max(0, value);
    partial void OnClipboardPollIntervalMillisecondsChanged(int value) => _clipboard.PollIntervalMilliseconds = Math.Max(10, value);
    partial void OnThemeChanged(string value) => _theme.SetTheme(value);
    [RelayCommand] private void Save() { _clipboard.IsEnabled = MonitorClipboard; }
}
