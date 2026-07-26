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
}