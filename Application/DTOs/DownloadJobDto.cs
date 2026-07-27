using Domain.Enums;

namespace Application.DTOs;

public record MediaSourceDto(
    Uri Url,
    Platform Platform,
    string Title,
    string Uploader,
    TimeSpan Duration,
    string? ThumbnailUrl);

public record MediaMetadataDto(
    string Title,
    string? Artist,
    string? Album,
    string? Genre,
    int? Year,
    uint? TrackNumber,
    bool HasCoverArt);

public record DownloadJobDto(
    Guid Id,
    MediaSourceDto Source,
    OutputFormat OutputFormat,
    DownloadStatus Status,
    string? OutputPath,
    MediaMetadataDto? Metadata,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);
