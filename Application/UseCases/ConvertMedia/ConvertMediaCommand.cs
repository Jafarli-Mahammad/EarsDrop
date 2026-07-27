using Application.Common.Models;
using Application.DTOs;
using MediatR;

namespace Application.UseCases.ConvertMedia;

public record ConvertMediaCommand(Guid JobId) : IRequest<Result<DownloadJobDto>>;
