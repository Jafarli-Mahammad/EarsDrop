using Application.Common.Models;
using Application.DTOs;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.FetchMetadata;

public class FetchMetadataHandler : IRequestHandler<FetchMetadataCommand, Result<MediaMetadataDto>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly IMetadataProvider _metadataProvider;
    private readonly ILogger<FetchMetadataHandler> _logger;

    public FetchMetadataHandler(
        IDownloadJobRepository jobRepository,
        IMetadataProvider metadataProvider,
        ILogger<FetchMetadataHandler> logger)
    {
        _jobRepository = jobRepository;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }

    public async Task<Result<MediaMetadataDto>> Handle(
        FetchMetadataCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing FetchMetadataCommand for JobId: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job == null)
        {
            return Result<MediaMetadataDto>.Failure(Error.NotFound);
        }

        try
        {
            job.FetchMetadata();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            var metadata = await _metadataProvider.GetMetadataAsync(job.Source, cancellationToken);
            if (metadata != null)
            {
                job.AttachMetadata(metadata);
                await _jobRepository.UpdateAsync(job, cancellationToken);
            }

            var dto = job.ToDto();
            if (dto.Metadata == null)
            {
                return Result<MediaMetadataDto>.Failure(Error.Custom("Metadata.NotFound", "No metadata could be found for this media."));
            }

            return Result<MediaMetadataDto>.Success(dto.Metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metadata for JobId: {JobId}", request.JobId);
            return Result<MediaMetadataDto>.Failure(Error.Custom("Metadata.Failed", ex.Message));
        }
    }
}
