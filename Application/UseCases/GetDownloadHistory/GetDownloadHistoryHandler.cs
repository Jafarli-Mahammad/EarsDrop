using Application.Common.Models;
using Application.DTOs;
using Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.GetDownloadHistory;

public class GetDownloadHistoryHandler : IRequestHandler<GetDownloadHistoryQuery, Result<IReadOnlyList<DownloadJobDto>>>
{
    private readonly IDownloadJobRepository _jobRepository;
    private readonly ILogger<GetDownloadHistoryHandler> _logger;

    public GetDownloadHistoryHandler(
        IDownloadJobRepository jobRepository,
        ILogger<GetDownloadHistoryHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<DownloadJobDto>>> Handle(
        GetDownloadHistoryQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving download job history...");

        var jobs = await _jobRepository.GetAllAsync(cancellationToken);
        var dtos = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => j.ToDto())
            .ToList();

        return Result<IReadOnlyList<DownloadJobDto>>.Success(dtos);
    }
}
