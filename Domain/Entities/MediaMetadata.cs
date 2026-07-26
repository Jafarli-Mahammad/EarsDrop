namespace Domain.Entities;

public class MediaMetadata
{
    public string Title { get; set; } = string.Empty;

    public string? Artist { get; set; }

    public string? Album { get; set; }

    public string? Genre { get; set; }

    public int? Year { get; set; }

    public uint? TrackNumber { get; set; }

    public byte[]? CoverArt { get; set; }
}