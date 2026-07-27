using Application.Common.Models;
using MediatR;

namespace Application.UseCases.DeleteDownload;

public record DeleteDownloadCommand(Guid JobId) : IRequest<Result<bool>>;
