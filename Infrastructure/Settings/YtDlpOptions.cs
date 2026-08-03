namespace Infrastructure.Settings;

public class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    public string ExecutablePath { get; set; } = "yt-dlp";

    public string ExtraArguments { get; set; } = string.Empty;

    // Optional: name or path mappings for JavaScript runtimes, e.g. "node" or "node:/usr/bin/node"
    public string JsRuntimes { get; set; } = string.Empty;

    // Optional: pass cookies from a browser profile, e.g. "chrome" or "firefox:default"
    public string CookiesFromBrowser { get; set; } = string.Empty;

    // Optional: path to a cookies.txt file (Netscape format)
    public string CookiesFile { get; set; } = string.Empty;

    // Optional: minimal backoff for HTTP 429 conditions
    public int RetriesOn429 { get; set; } = 2;
    public int RetryDelaySecondsOn429 { get; set; } = 5;
}