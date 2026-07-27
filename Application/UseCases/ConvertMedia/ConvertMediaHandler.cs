using Application.Common.Models;
using Application.DTOs;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ConvertMedia;

public class ConvertMediaHandler : IRequestHandler<ConvertMediaCommand, Result<DownloadJobDto>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly IMediaConverter _converter;
    private readonly ILogger<ConvertMediaHandler> _logger;

    public ConvertMediaHandler(
        IDownloadJobRepository jobRepository,
        IMediaConverter converter,
        ILogger<ConvertMediaHandler> logger)
    {
        _jobRepository = jobRepository;
        _converter = converter;
        _logger = logger;
    }

    public async Task<Result<DownloadJobDto>> Handle(
        ConvertMediaCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing ConvertMediaCommand for JobId: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job == null)
        {
            return Result<DownloadJobDto>.Failure(Error.NotFound);
        }

        try
        {
            await _converter.ConvertAsync(job, cancellationToken);
            await _jobRepository.UpdateAsync(job, cancellationToken);
            return Result<DownloadJobDto>.Success(job.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert media for JobId: {JobId}", request.JobId);
            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, CancellationToken.None);
            return Result<DownloadJobDto>.Failure(Error.Custom("Conversion.Failed", ex.Message));
        }
    }
}
