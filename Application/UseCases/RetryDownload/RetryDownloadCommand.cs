using Application.Common.Models;
using Application.DTOs;
using MediatR;

namespace Application.UseCases.RetryDownload;

public record RetryDownloadCommand(Guid JobId) : IRequest<Result<DownloadJobDto>>;
