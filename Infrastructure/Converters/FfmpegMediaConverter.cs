using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.Value_Objects;
using Infrastructure.ProcessRunner;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Converters;

public class FfmpegMediaConverter : IMediaConverter
{
    private readonly IProcessRunner _processRunner;
    private readonly FfmpegOptions _ffmpegOptions;
    private readonly ILogger<FfmpegMediaConverter> _logger;

    public FfmpegMediaConverter(
        IProcessRunner processRunner,
        IOptions<FfmpegOptions> ffmpegOptions,
        ILogger<FfmpegMediaConverter> logger)
    {
        _processRunner = processRunner;
        _ffmpegOptions = ffmpegOptions.Value;
        _logger = logger;
    }

    public async Task ConvertAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        if (job.OutputPath == null || string.IsNullOrWhiteSpace(job.OutputPath.Value))
        {
            throw new ConversionException("Cannot convert job without a valid OutputPath.");
        }

        var sourcePath = job.OutputPath.Value;
        if (!File.Exists(sourcePath))
        {
            throw new ConversionException($"Input file for conversion not found at '{sourcePath}'.");
        }

        var targetExtension = job.OutputFormat switch
        {
            OutputFormat.Mp3 => ".mp3",
            OutputFormat.Mp4 => ".mp4",
            _ => ".mp3"
        };

        var currentExtension = Path.GetExtension(sourcePath);
        if (string.Equals(currentExtension, targetExtension, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("File '{SourcePath}' is already in target format '{Extension}'", sourcePath, targetExtension);
            return;
        }

        var targetPath = Path.ChangeExtension(sourcePath, targetExtension);
        _logger.LogInformation("Converting '{SourcePath}' to '{TargetPath}' using FFmpeg", sourcePath, targetPath);

        string arguments = job.OutputFormat switch
        {
            OutputFormat.Mp3 => $"-y -i \"{sourcePath}\" -vn -ar 44100 -ac 2 -c:a {_ffmpegOptions.AudioCodec} -b:a {_ffmpegOptions.AudioBitrate} \"{targetPath}\"",
            OutputFormat.Mp4 => $"-y -i \"{sourcePath}\" -c:v libx264 -c:a aac -strict experimental \"{targetPath}\"",
            _ => $"-y -i \"{sourcePath}\" -vn -ar 44100 -ac 2 -c:a {_ffmpegOptions.AudioCodec} -b:a {_ffmpegOptions.AudioBitrate} \"{targetPath}\""
        };

        var result = await _processRunner.ExecuteAsync(_ffmpegOptions.ExecutablePath, arguments, Path.GetDirectoryName(targetPath), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("FFmpeg conversion failed: {Error}", result.StandardError);
            throw new ConversionException($"FFmpeg conversion failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        if (!File.Exists(targetPath))
        {
            throw new ConversionException($"Converted output file '{targetPath}' was not created.");
        }

        // Clean up raw file if different from target
        try
        {
            if (File.Exists(sourcePath) && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(sourcePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary pre-converted file '{SourcePath}'", sourcePath);
        }

        job.MarkConverted(new FilePath(targetPath));
        _logger.LogInformation("Conversion completed successfully: {TargetPath}", targetPath);
    }
}