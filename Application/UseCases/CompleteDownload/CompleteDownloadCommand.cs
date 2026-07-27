using Application.Common.Models;
using Application.DTOs;
using MediatR;

namespace Application.UseCases.CompleteDownload;

public record CompleteDownloadCommand(Guid JobId) : IRequest<Result<DownloadJobDto>>;
