using Application.Common.Models;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.WriteMetadata;

public class WriteMetadataHandler : IRequestHandler<WriteMetadataCommand, Result<bool>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly IMetadataWriter _metadataWriter;
    private readonly ILogger<WriteMetadataHandler> _logger;

    public WriteMetadataHandler(
        IDownloadJobRepository jobRepository,
        IMetadataWriter metadataWriter,
        ILogger<WriteMetadataHandler> logger)
    {
        _jobRepository = jobRepository;
        _metadataWriter = metadataWriter;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        WriteMetadataCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing WriteMetadataCommand for JobId: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job == null)
        {
            return Result<bool>.Failure(Error.NotFound);
        }

        if (job.Metadata == null)
        {
            return Result<bool>.Failure(Error.Custom("Metadata.Missing", "No metadata attached to this job."));
        }

        if (job.OutputPath == null)
        {
            return Result<bool>.Failure(Error.Custom("Metadata.OutputMissing", "Job output path is missing."));
        }

        try
        {
            await _metadataWriter.WriteMetadataAsync(job, cancellationToken);
            await _jobRepository.UpdateAsync(job, cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write metadata for JobId: {JobId}", request.JobId);
            return Result<bool>.Failure(Error.Custom("Metadata.WriteFailed", ex.Message));
        }
    }
}
