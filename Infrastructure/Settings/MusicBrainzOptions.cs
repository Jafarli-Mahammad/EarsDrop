namespace Infrastructure.Settings;

public class MusicBrainzOptions
{
    public const string SectionName = "MusicBrainz";

    public string BaseUrl { get; set; } = "https://musicbrainz.org/ws/2/";

    public string UserAgent { get; set; } = "EarsDrop/1.0.0 ( contact@earsdrop.app )";

    public string CoverArtBaseUrl { get; set; } = "https://coverartarchive.org/release/";
}