using Application.Common.Models;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.DownloadMedia;

public class DownloadMediaHandler : IRequestHandler<DownloadMediaCommand, Result<DownloadJobDto>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly IMediaDownloader _downloader;
    private readonly IMediaConverter _converter;
    private readonly IMetadataProvider _metadataProvider;
    private readonly IMetadataWriter _metadataWriter;
    private readonly ILogger<DownloadMediaHandler> _logger;

    public DownloadMediaHandler(
        IDownloadJobRepository jobRepository,
        IMediaDownloader downloader,
        IMediaConverter converter,
        IMetadataProvider metadataProvider,
        IMetadataWriter metadataWriter,
        ILogger<DownloadMediaHandler> logger)
    {
        _jobRepository = jobRepository;
        _downloader = downloader;
        _converter = converter;
        _metadataProvider = metadataProvider;
        _metadataWriter = metadataWriter;
        _logger = logger;
    }

    public async Task<Result<DownloadJobDto>> Handle(
        DownloadMediaCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting download media process for URL: {Url}", request.Url);

        // Defensive parse: although DownloadMediaValidator runs first in the pipeline, do not
        // rely on ordering. If invoked directly (tests / composition), fail gracefully instead
        // of throwing an unhandled UriFormatException.
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            return Result<DownloadJobDto>.Failure(
                Error.Custom("Download.InvalidUrl", $"The URL '{request.Url}' is not a valid absolute URI."));
        }

        var platform = DetectPlatform(uri);

        var mediaSource = new MediaSource
        {
            Url = uri,
            Platform = platform,
            Title = uri.AbsolutePath.Trim('/'),
            Uploader = "Unknown",
            Duration = TimeSpan.Zero
        };

        var job = new DownloadJob
        {
            Source = mediaSource,
            OutputFormat = request.OutputFormat
        };

        await _jobRepository.AddAsync(job, cancellationToken);

        try
        {
            // 1. Download
            _logger.LogInformation("Job {JobId}: Starting yt-dlp download...", job.Id);
            job.Start();
            await _jobRepository.UpdateAsync(job, cancellationToken);
            await _downloader.DownloadAsync(job, cancellationToken);

            // 2. Convert
            _logger.LogInformation("Job {JobId}: Converting media to format {Format}...", job.Id, job.OutputFormat);
            await _converter.ConvertAsync(job, cancellationToken);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            // 3. Build metadata from yt-dlp probe data.
            var metadata = BuildMetadataFromSource(job.Source, request.EnableCoverArtEmbedding);
            var shouldEnrichFromMusicBrainz = request.EnableMetadataEnrichment || !HasEssentialMetadata(metadata);

            if (shouldEnrichFromMusicBrainz)
            {
                _logger.LogInformation("Job {JobId}: Fetching metadata from MusicBrainz...", job.Id);
                job.FetchMetadata();
                await _jobRepository.UpdateAsync(job, cancellationToken);

                try
                {
                    var enrichedMetadata = await _metadataProvider.GetMetadataAsync(job.Source, cancellationToken);
                    metadata = MergeMetadata(metadata, enrichedMetadata, request.EnableCoverArtEmbedding);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Metadata fetch encountered an issue, proceeding with yt-dlp metadata", job.Id);
                }
            }

            // 4. Write Metadata
            if (metadata != null)
            {
                job.AttachMetadata(metadata);
                await _jobRepository.UpdateAsync(job, cancellationToken);
            }

            if (request.EnableTagWriting && job.Metadata != null && job.OutputPath != null)
            {
                _logger.LogInformation("Job {JobId}: Writing metadata tags...", job.Id);
                await _metadataWriter.WriteMetadataAsync(job, cancellationToken);
            }

            // 5. Complete & Save History
            _logger.LogInformation("Job {JobId}: Successfully completed download pipeline", job.Id);
            job.Complete();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            return Result<DownloadJobDto>.Success(job.ToDto());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job {JobId}: Download process was cancelled", job.Id);
            job.Cancel();
            await _jobRepository.UpdateAsync(job, CancellationToken.None);
            return Result<DownloadJobDto>.Failure(Error.Custom("Download.Cancelled", "The download job was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId}: Download pipeline failed", job.Id);
            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, CancellationToken.None);
            return Result<DownloadJobDto>.Failure(Error.Custom("Download.Failed", ex.Message));
        }
    }

    private static MediaMetadata BuildMetadataFromSource(MediaSource source, bool includeCoverArt)
    {
        return new MediaMetadata
        {
            Title = source.Title,
            Artist = !string.IsNullOrWhiteSpace(source.Artist) ? source.Artist : source.Uploader,
            Album = source.Album,
            Genre = source.Genre,
            Year = source.Year,
            TrackNumber = source.TrackNumber,
            CoverArt = includeCoverArt ? source.ThumbnailData : null
        };
    }

    private static bool HasEssentialMetadata(MediaMetadata metadata)
    {
        return !string.IsNullOrWhiteSpace(metadata.Title)
            && !string.IsNullOrWhiteSpace(metadata.Artist);
    }

    private static MediaMetadata MergeMetadata(MediaMetadata current, MediaMetadata? enriched, bool includeCoverArt)
    {
        if (enriched is null)
        {
            return current;
        }

        return new MediaMetadata
        {
            Title = !string.IsNullOrWhiteSpace(current.Title) ? current.Title : enriched.Title,
            Artist = !string.IsNullOrWhiteSpace(current.Artist) ? current.Artist : enriched.Artist,
            Album = !string.IsNullOrWhiteSpace(current.Album) ? current.Album : enriched.Album,
            Genre = !string.IsNullOrWhiteSpace(current.Genre) ? current.Genre : enriched.Genre,
            Year = current.Year ?? enriched.Year,
            TrackNumber = current.TrackNumber ?? enriched.TrackNumber,
            CoverArt = includeCoverArt
                ? (current.CoverArt is { Length: > 0 } ? current.CoverArt : enriched.CoverArt)
                : null
        };
    }

    private static Platform DetectPlatform(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            return Platform.YouTube;
        if (host.Contains("soundcloud.com"))
            return Platform.SoundCloud;
        if (host.Contains("vimeo.com"))
            return Platform.Vimeo;
        if (host.Contains("bandcamp.com"))
            return Platform.Bandcamp;

        return Platform.Unknown;
    }
}
