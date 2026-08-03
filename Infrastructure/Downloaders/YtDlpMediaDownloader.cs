using System.Text.Json;
using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.Value_Objects;
using Infrastructure.ProcessRunner;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Downloaders;

public class YtDlpMediaDownloader : IMediaDownloader
{
    private readonly IProcessRunner _processRunner;
    private readonly HttpClient _httpClient;
    private readonly YtDlpOptions _ytDlpOptions;
    private readonly DownloadOptions _downloadOptions;
    private readonly ILogger<YtDlpMediaDownloader> _logger;

    public YtDlpMediaDownloader(
        IProcessRunner processRunner,
        HttpClient httpClient,
        IOptions<YtDlpOptions> ytDlpOptions,
        IOptions<DownloadOptions> downloadOptions,
        ILogger<YtDlpMediaDownloader> logger)
    {
        _processRunner = processRunner;
        _httpClient = httpClient;
        _ytDlpOptions = ytDlpOptions.Value;
        _downloadOptions = downloadOptions.Value;
        _logger = logger;
    }

    public async Task DownloadAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading media from URL: {Url}", job.Source.Url);

        Directory.CreateDirectory(_downloadOptions.OutputDirectory);

        // Step 1 and step 2 can overlap: the metadata probe does not depend on the
        // downloaded file, so run it in parallel instead of forcing users to wait
        // for a second yt-dlp process before the real download starts.
        var sourceMetadataTask = PopulateSourceMetadataAsync(job, cancellationToken);

        // Step 2: Download Media
        var outputTemplate = Path.Combine(_downloadOptions.OutputDirectory, "%(title)s.%(ext)s");
        var sanitizeTemplate = outputTemplate.Replace("\"", "\\\"");

        string formatArgs = job.OutputFormat switch
        {
            OutputFormat.Mp3 => "-x --audio-format mp3 --audio-quality 0",
            OutputFormat.Mp4 => "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"",
            _ => "-x --audio-format mp3"
        };

        // Build initial arguments with hardening, plus optional cookies/js runtime.
        string BuildArguments()
        {
            // Use after_move:filepath so yt-dlp prints the final path after any post-processing/moves
            // (important when using -x audio extraction which changes extension and location).
            var args = $"--no-playlist --print after_move:filepath {formatArgs} -o \"{sanitizeTemplate}\"";
            if (!string.IsNullOrWhiteSpace(_ytDlpOptions.JsRuntimes))
            {
                args += $" --js-runtimes {_ytDlpOptions.JsRuntimes}";
            }
            if (!string.IsNullOrWhiteSpace(_ytDlpOptions.CookiesFromBrowser))
            {
                args += $" --cookies-from-browser {_ytDlpOptions.CookiesFromBrowser}";
            }
            else if (!string.IsNullOrWhiteSpace(_ytDlpOptions.CookiesFile))
            {
                // Don't require the file to exist at build-time; yt-dlp will error clearly if missing.
                var cookiesEscaped = _ytDlpOptions.CookiesFile.Replace("\"", "\\\"");
                args += $" --cookies \"{cookiesEscaped}\"";
            }
            if (!string.IsNullOrWhiteSpace(_ytDlpOptions.ExtraArguments))
            {
                args += $" {_ytDlpOptions.ExtraArguments}";
            }
            // The trailing "--" ensures the URL (which originates from untrusted clipboard/paste
            // input) is always treated as a positional argument and can never be interpreted as a yt-dlp flag.
            args += $" -- \"{job.Source.Url}\"";
            return args;
        }

        bool IsRateLimit(string? stderr)
            => !string.IsNullOrWhiteSpace(stderr) && (stderr.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase)
                || stderr.Contains("Sign in to confirm you’re not a bot", StringComparison.OrdinalIgnoreCase)
                || stderr.Contains("Sign in to confirm you're not a bot", StringComparison.OrdinalIgnoreCase));

        int attempts = Math.Max(0, _ytDlpOptions.RetriesOn429) + 1;
        Exception? lastError = null;
        string? lastStdErr = null;
        string? lastStdOut = null;
        for (var tryIndex = 0; tryIndex < attempts; tryIndex++)
        {
            var arguments = BuildArguments();
            var result = await _processRunner.ExecuteAsync(_ytDlpOptions.ExecutablePath, arguments, _downloadOptions.OutputDirectory, cancellationToken);
            lastStdErr = result.StandardError;
            lastStdOut = result.StandardOutput;
            if (result.IsSuccess)
            {
                // Proceed to filename resolution below with lastStdOut.
                break;
            }

            // If rate-limited, optionally back off and retry.
            if (IsRateLimit(result.StandardError) && tryIndex < attempts - 1)
            {
                _logger.LogWarning("yt-dlp rate-limited (HTTP 429). Backing off before retry {Attempt}/{Total}.", tryIndex + 2, attempts);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _ytDlpOptions.RetryDelaySecondsOn429)), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                continue;
            }

            // Non-retryable failure or out of retries
            _logger.LogError("yt-dlp failed: {Error}", result.StandardError);
            lastError = new DownloadException($"yt-dlp download failed with exit code {result.ExitCode}: {result.StandardError}");
            break;
        }

        if (lastError != null)
        {
            // Provide actionable guidance if common causes detected.
            var guidance = string.Empty;
            if (IsRateLimit(lastStdErr))
            {
                guidance = " YouTube responded with 429 (Too Many Requests). Try configuring cookies (YtDlp:CookiesFromBrowser or CookiesFile) or waiting and retrying.";
            }
            else if (!string.IsNullOrWhiteSpace(lastStdErr) && lastStdErr.Contains("No supported JavaScript runtime could be found", StringComparison.OrdinalIgnoreCase))
            {
                guidance = " yt-dlp needs a JavaScript runtime. Install Node.js or Deno and set YtDlp:JsRuntimes (e.g., 'node') in settings.";
            }
            throw new DownloadException((lastError.Message + guidance).Trim());
        }

        var expectedFile = (lastStdOut ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();

        // yt-dlp's `--print filename` can output just the base name. If so, resolve it
        // against the configured output directory to get the actual file path.
        if (!string.IsNullOrWhiteSpace(expectedFile) && !Path.IsPathRooted(expectedFile))
        {
            expectedFile = Path.GetFullPath(Path.Combine(_downloadOptions.OutputDirectory, expectedFile));
        }

        if (string.IsNullOrEmpty(expectedFile) || !File.Exists(expectedFile))
        {
            _logger.LogWarning("yt-dlp did not yield a resolvable file path. stdout: {StdOut}. stderr: {StdErr}", lastStdOut, lastStdErr);
            // Fallback search in output directory for latest file
            var directoryInfo = new DirectoryInfo(_downloadOptions.OutputDirectory);
            var lastFile = directoryInfo.GetFiles()
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (lastFile != null)
            {
                expectedFile = lastFile.FullName;
            }
            else
            {
                throw new DownloadException("Downloaded file could not be located after yt-dlp execution.");
            }
        }

        await sourceMetadataTask;

        job.MarkConverted(new FilePath(expectedFile));
        _logger.LogInformation("Download completed successfully: {Path}", expectedFile);
    }

    private async Task PopulateSourceMetadataAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        try
        {
            var dumpArgs = "--dump-json --no-playlist";
            if (!string.IsNullOrWhiteSpace(_ytDlpOptions.JsRuntimes))
            {
                dumpArgs += $" --js-runtimes {_ytDlpOptions.JsRuntimes}";
            }
            if (!string.IsNullOrWhiteSpace(_ytDlpOptions.CookiesFromBrowser))
            {
                dumpArgs += $" --cookies-from-browser {_ytDlpOptions.CookiesFromBrowser}";
            }
            else if (!string.IsNullOrWhiteSpace(_ytDlpOptions.CookiesFile))
            {
                var cookiesEscaped = _ytDlpOptions.CookiesFile.Replace("\"", "\\\"");
                dumpArgs += $" --cookies \"{cookiesEscaped}\"";
            }
            dumpArgs += $" -- \"{job.Source.Url}\"";
            var dumpResult = await _processRunner.ExecuteAsync(_ytDlpOptions.ExecutablePath, dumpArgs, _downloadOptions.OutputDirectory, cancellationToken);

            if (dumpResult.IsSuccess && !string.IsNullOrWhiteSpace(dumpResult.StandardOutput))
            {
                using var jsonDoc = JsonDocument.Parse(dumpResult.StandardOutput);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Title = titleProp.GetString() ?? job.Source.Title;
                }

                if (root.TryGetProperty("uploader", out var uploaderProp) && uploaderProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Uploader = uploaderProp.GetString() ?? job.Source.Uploader;
                }
                else if (root.TryGetProperty("artist", out var artistProp) && artistProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Uploader = artistProp.GetString() ?? job.Source.Uploader;
                }

                if (root.TryGetProperty("artist", out var sourceArtistProp) && sourceArtistProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Artist = sourceArtistProp.GetString();
                }

                if (root.TryGetProperty("album", out var albumProp) && albumProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Album = albumProp.GetString();
                }

                if (root.TryGetProperty("genre", out var genreProp) && genreProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.Genre = genreProp.GetString();
                }

                if (root.TryGetProperty("track_number", out var trackNumberProp) && trackNumberProp.ValueKind == JsonValueKind.Number)
                {
                    var track = trackNumberProp.GetInt32();
                    if (track > 0)
                    {
                        job.Source.TrackNumber = (uint)track;
                    }
                }

                if (root.TryGetProperty("release_year", out var releaseYearProp) && releaseYearProp.ValueKind == JsonValueKind.Number)
                {
                    job.Source.Year = releaseYearProp.GetInt32();
                }
                else if (root.TryGetProperty("upload_date", out var uploadDateProp) && uploadDateProp.ValueKind == JsonValueKind.String)
                {
                    var uploadDate = uploadDateProp.GetString();
                    if (!string.IsNullOrWhiteSpace(uploadDate) && uploadDate.Length >= 4 && int.TryParse(uploadDate[..4], out var year))
                    {
                        job.Source.Year = year;
                    }
                }

                if (root.TryGetProperty("duration", out var durationProp) && durationProp.ValueKind == JsonValueKind.Number)
                {
                    job.Source.Duration = TimeSpan.FromSeconds(durationProp.GetDouble());
                }

                if (root.TryGetProperty("thumbnail", out var thumbProp) && thumbProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.ThumbnailUrl = thumbProp.GetString();
                    job.Source.ThumbnailData = await TryDownloadThumbnailAsync(job.Source.ThumbnailUrl, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Honor cooperative cancellation instead of silently swallowing it.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract pre-download JSON metadata for {Url}", job.Source.Url);
        }
    }

    private async Task<byte[]?> TryDownloadThumbnailAsync(string? thumbnailUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(thumbnailUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}