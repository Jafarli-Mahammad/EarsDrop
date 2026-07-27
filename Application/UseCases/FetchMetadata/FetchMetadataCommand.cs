using Application.Common.Models;
using Application.DTOs;
using MediatR;

namespace Application.UseCases.FetchMetadata;

public record FetchMetadataCommand(Guid JobId) : IRequest<Result<MediaMetadataDto>>;
