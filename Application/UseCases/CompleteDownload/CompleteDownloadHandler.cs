using Application.Common.Models;
using Application.DTOs;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.CompleteDownload;

public class CompleteDownloadHandler : IRequestHandler<CompleteDownloadCommand, Result<DownloadJobDto>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly ILogger<CompleteDownloadHandler> _logger;

    public CompleteDownloadHandler(
        IDownloadJobRepository jobRepository,
        ILogger<CompleteDownloadHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<Result<DownloadJobDto>> Handle(
        CompleteDownloadCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing CompleteDownloadCommand for JobId: {JobId}", request.JobId);

        try
        {
            var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
            if (job == null)
            {
                return Result<DownloadJobDto>.Failure(Error.NotFound);
            }

            job.Complete();
            await _jobRepository.UpdateAsync(job, cancellationToken);

            return Result<DownloadJobDto>.Success(job.ToDto());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete download job {JobId}", request.JobId);
            return Result<DownloadJobDto>.Failure(Error.Custom("Download.CompleteFailed", ex.Message));
        }
    }
}
