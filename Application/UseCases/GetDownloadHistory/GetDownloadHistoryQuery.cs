using Application.Common.Models;
using Application.DTOs;
using MediatR;

namespace Application.UseCases.GetDownloadHistory;

public record GetDownloadHistoryQuery() : IRequest<Result<IReadOnlyList<DownloadJobDto>>>;
