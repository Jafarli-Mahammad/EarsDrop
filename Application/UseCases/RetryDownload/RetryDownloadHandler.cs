using Application.Common.Models;
using Application.DTOs;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.RetryDownload;

public class RetryDownloadHandler : IRequestHandler<RetryDownloadCommand, Result<DownloadJobDto>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly IMediaDownloader _downloader;
    private readonly IMediaConverter _converter;
    private readonly IMetadataProvider _metadataProvider;
    private readonly IMetadataWriter _metadataWriter;
    private readonly ILogger<RetryDownloadHandler> _logger;

    public RetryDownloadHandler(
        IDownloadJobRepository jobRepository,
        IMediaDownloader downloader,
        IMediaConverter converter,
        IMetadataProvider metadataProvider,
        IMetadataWriter metadataWriter,
        ILogger<RetryDownloadHandler> logger)
    {
        _jobRepository = jobRepository;
        _downloader = downloader;
        _converter = converter;
        _metadataProvider = metadataProvider;
        _metadataWriter = metadataWriter;
        _logger = logger;
    }

    public async Task<Result<DownloadJobDto>> Handle(
        RetryDownloadCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing RetryDownloadCommand for JobId: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job == null)
        {
            return Result<DownloadJobDto>.Failure(Error.NotFound);
        }

        try
        {
            _logger.LogInformation("Retrying download job {JobId}", job.Id);
            job.Start();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            await _downloader.DownloadAsync(job, cancellationToken);
            await _converter.ConvertAsync(job, cancellationToken);

            job.FetchMetadata();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            try
            {
                var metadata = await _metadataProvider.GetMetadataAsync(job.Source, cancellationToken);
                if (metadata != null)
                {
                    job.AttachMetadata(metadata);
                    await _jobRepository.UpdateAsync(job, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metadata fetch failed during retry for JobId: {JobId}", job.Id);
            }

            if (job.Metadata != null && job.OutputPath != null)
            {
                await _metadataWriter.WriteMetadataAsync(job, cancellationToken);
            }

            job.Complete();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            return Result<DownloadJobDto>.Success(job.ToDto());
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
            await _jobRepository.UpdateAsync(job, CancellationToken.None);
            return Result<DownloadJobDto>.Failure(Error.Custom("Download.Cancelled", "Retry was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry failed for JobId: {JobId}", job.Id);
            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, CancellationToken.None);
            return Result<DownloadJobDto>.Failure(Error.Custom("Download.RetryFailed", ex.Message));
        }
    }
}
