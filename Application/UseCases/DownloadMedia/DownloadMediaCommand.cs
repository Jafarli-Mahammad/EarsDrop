using Application.Common.Models;
using Application.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.DownloadMedia;

public record DownloadMediaCommand(
    string Url,
    OutputFormat OutputFormat = OutputFormat.Mp3,
    bool EnableMetadataEnrichment = false,
    bool EnableCoverArtEmbedding = true,
    bool EnableTagWriting = true) : IRequest<Result<DownloadJobDto>>;
