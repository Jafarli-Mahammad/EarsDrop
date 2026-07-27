using Application.Common.Models;
using MediatR;

namespace Application.UseCases.WriteMetadata;

public record WriteMetadataCommand(Guid JobId) : IRequest<Result<bool>>;
