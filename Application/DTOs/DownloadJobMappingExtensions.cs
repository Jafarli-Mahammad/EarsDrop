using Domain.Entities;

namespace Application.DTOs;

public static class DownloadJobMappingExtensions
{
    public static DownloadJobDto ToDto(this DownloadJob job)
    {
        var sourceDto = new MediaSourceDto(
            job.Source.Url,
            job.Source.Platform,
            job.Source.Title,
            job.Source.Uploader,
            job.Source.Duration,
            job.Source.ThumbnailUrl);

        MediaMetadataDto? metadataDto = null;
        if (job.Metadata != null)
        {
            metadataDto = new MediaMetadataDto(
                job.Metadata.Title,
                job.Metadata.Artist,
                job.Metadata.Album,
                job.Metadata.Genre,
                job.Metadata.Year,
                job.Metadata.TrackNumber,
                job.Metadata.CoverArt != null && job.Metadata.CoverArt.Length > 0);
        }

        return new DownloadJobDto(
            job.Id,
            sourceDto,
            job.OutputFormat,
            job.Status,
            job.OutputPath?.Value,
            metadataDto,
            job.CreatedAt,
            job.CompletedAt,
            job.ErrorMessage);
    }
}
