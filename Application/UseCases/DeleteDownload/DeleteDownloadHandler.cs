using Application.Common.Models;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.DeleteDownload;

public class DeleteDownloadHandler : IRequestHandler<DeleteDownloadCommand, Result<bool>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly ILogger<DeleteDownloadHandler> _logger;

    public DeleteDownloadHandler(
        IDownloadJobRepository jobRepository,
        ILogger<DeleteDownloadHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteDownloadCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing DeleteDownloadCommand for JobId: {JobId}", request.JobId);

        var existing = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (existing == null)
        {
            return Result<bool>.Failure(Error.NotFound);
        }

        await _jobRepository.DeleteAsync(request.JobId, cancellationToken);
        return Result<bool>.Success(true);
    }
}
