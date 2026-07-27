namespace Infrastructure.Settings;

public class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; set; } = "yt-dlp";

    public string ExtraArguments { get; set; } = string.Empty;
}