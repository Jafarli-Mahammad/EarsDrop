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
    private readonly YtDlpOptions _ytDlpOptions;
    private readonly DownloadOptions _downloadOptions;
    private readonly ILogger<YtDlpMediaDownloader> _logger;

    public YtDlpMediaDownloader(
        IProcessRunner processRunner,
        IOptions<YtDlpOptions> ytDlpOptions,
        IOptions<DownloadOptions> downloadOptions,
        ILogger<YtDlpMediaDownloader> logger)
    {
        _processRunner = processRunner;
        _ytDlpOptions = ytDlpOptions.Value;
        _downloadOptions = downloadOptions.Value;
        _logger = logger;
    }

    public async Task DownloadAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading media from URL: {Url}", job.Source.Url);

        Directory.CreateDirectory(_downloadOptions.OutputDirectory);

        // Step 1: Extract Metadata JSON
        await PopulateSourceMetadataAsync(job, cancellationToken);

        // Step 2: Download Media
        var outputTemplate = Path.Combine(_downloadOptions.OutputDirectory, "%(title)s.%(ext)s");
        var sanitizeTemplate = outputTemplate.Replace("\"", "\\\"");

        string formatArgs = job.OutputFormat switch
        {
            OutputFormat.Mp3 => "-x --audio-format mp3 --audio-quality 0",
            OutputFormat.Mp4 => "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"",
            _ => "-x --audio-format mp3"
        };

        var arguments = $"--no-playlist --print filename {formatArgs} -o \"{sanitizeTemplate}\" \"{job.Source.Url}\"";
        if (!string.IsNullOrWhiteSpace(_ytDlpOptions.ExtraArguments))
        {
            arguments += $" {_ytDlpOptions.ExtraArguments}";
        }

        var result = await _processRunner.ExecuteAsync(_ytDlpOptions.ExecutablePath, arguments, _downloadOptions.OutputDirectory, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("yt-dlp failed: {Error}", result.StandardError);
            throw new DownloadException($"yt-dlp download failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        var expectedFile = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();

        if (string.IsNullOrEmpty(expectedFile) || !File.Exists(expectedFile))
        {
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

        job.MarkConverted(new FilePath(expectedFile));
        _logger.LogInformation("Download completed successfully: {Path}", expectedFile);
    }

    private async Task PopulateSourceMetadataAsync(DownloadJob job, CancellationToken cancellationToken)
    {
        try
        {
            var dumpArgs = $"--dump-json --no-playlist \"{job.Source.Url}\"";
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

                if (root.TryGetProperty("duration", out var durationProp) && durationProp.ValueKind == JsonValueKind.Number)
                {
                    job.Source.Duration = TimeSpan.FromSeconds(durationProp.GetDouble());
                }

                if (root.TryGetProperty("thumbnail", out var thumbProp) && thumbProp.ValueKind == JsonValueKind.String)
                {
                    job.Source.ThumbnailUrl = thumbProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract pre-download JSON metadata for {Url}", job.Source.Url);
        }
    }
}