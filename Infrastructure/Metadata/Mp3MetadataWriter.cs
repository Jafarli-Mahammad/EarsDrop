using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Metadata;

public class Mp3MetadataWriter : IMetadataWriter
{
    private readonly ILogger<Mp3MetadataWriter> _logger;

    public Mp3MetadataWriter(ILogger<Mp3MetadataWriter> logger)
    {
        _logger = logger;
    }

    public Task WriteMetadataAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        if (job.OutputPath == null || string.IsNullOrWhiteSpace(job.OutputPath.Value))
        {
            throw new MetadataException("Cannot write metadata: Job OutputPath is missing.");
        }

        var filePath = job.OutputPath.Value;
        if (!File.Exists(filePath))
        {
            throw new MetadataException($"Target audio file '{filePath}' does not exist.");
        }

        if (job.Metadata == null)
        {
            _logger.LogWarning("No metadata object provided to write for file '{FilePath}'", filePath);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Writing TagLib# metadata tags to file: {FilePath}", filePath);

        try
        {
            using var file = TagLib.File.Create(filePath);

            if (!string.IsNullOrWhiteSpace(job.Metadata.Title))
            {
                file.Tag.Title = job.Metadata.Title;
            }

            if (!string.IsNullOrWhiteSpace(job.Metadata.Artist))
            {
                file.Tag.Performers = new[] { job.Metadata.Artist };
            }

            if (!string.IsNullOrWhiteSpace(job.Metadata.Album))
            {
                file.Tag.Album = job.Metadata.Album;
            }

            if (!string.IsNullOrWhiteSpace(job.Metadata.Genre))
            {
                file.Tag.Genres = new[] { job.Metadata.Genre };
            }

            if (job.Metadata.Year.HasValue && job.Metadata.Year.Value > 0)
            {
                file.Tag.Year = (uint)job.Metadata.Year.Value;
            }

            if (job.Metadata.TrackNumber.HasValue && job.Metadata.TrackNumber.Value > 0)
            {
                file.Tag.Track = job.Metadata.TrackNumber.Value;
            }

            if (job.Metadata.CoverArt != null && job.Metadata.CoverArt.Length > 0)
            {
                var picture = new TagLib.Picture(new TagLib.ByteVector(job.Metadata.CoverArt))
                {
                    Type = TagLib.PictureType.FrontCover,
                    Description = "Front Cover",
                    MimeType = "image/jpeg"
                };

                file.Tag.Pictures = new TagLib.IPicture[] { picture };
            }

            file.Save();
            _logger.LogInformation("Successfully saved TagLib# ID3 metadata tags for file '{FilePath}'", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write metadata tags to audio file '{FilePath}'", filePath);
            throw new MetadataException($"Failed to write ID3 metadata tags to '{filePath}': {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }
}
