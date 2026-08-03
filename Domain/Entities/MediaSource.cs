using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class MediaSource : Entity
{
    public Uri Url { get; set; } = default!;

    public Platform Platform { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Uploader { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? Artist { get; set; }

    public string? Album { get; set; }

    public string? Genre { get; set; }

    public int? Year { get; set; }

    public uint? TrackNumber { get; set; }

    public byte[]? ThumbnailData { get; set; }
}