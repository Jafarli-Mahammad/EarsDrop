using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Metadata;

public class MusicBrainzMetadataProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;
    private readonly MusicBrainzOptions _options;
    private readonly ILogger<MusicBrainzMetadataProvider> _logger;

    public MusicBrainzMetadataProvider(
        HttpClient httpClient,
        IOptions<MusicBrainzOptions> options,
        ILogger<MusicBrainzMetadataProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress == null && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<MediaMetadata> GetMetadataAsync(MediaSource source, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching metadata from MusicBrainz for: {Title}", source.Title);

        var metadata = new MediaMetadata
        {
            Title = source.Title,
            Artist = source.Uploader
        };

        var (searchArtist, searchTrack) = ExtractArtistAndTrack(source.Title, source.Uploader);

        try
        {
            var query = !string.IsNullOrWhiteSpace(searchArtist)
                ? $"recording:\"{searchTrack}\" AND artist:\"{searchArtist}\""
                : $"recording:\"{searchTrack}\"";

            var requestUrl = $"recording?query={Uri.EscapeDataString(query)}&fmt=json&limit=1";
            var response = await _httpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MusicBrainz API returned status code {StatusCode}", response.StatusCode);
                return metadata;
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);

            var root = doc.RootElement;
            if (!root.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
            {
                _logger.LogInformation("No recordings found on MusicBrainz for query: {Query}", query);
                return metadata;
            }

            var bestMatch = recordings[0];

            // Title
            if (bestMatch.TryGetProperty("title", out var titleProp))
            {
                metadata.Title = titleProp.GetString() ?? metadata.Title;
            }

            // Artist
            if (bestMatch.TryGetProperty("artist-credit", out var artistCredits) && artistCredits.GetArrayLength() > 0)
            {
                var artistName = artistCredits[0].GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(artistName))
                {
                    metadata.Artist = artistName;
                }
            }

            // Releases (Album, Year, Release ID for Cover Art)
            string? releaseId = null;
            if (bestMatch.TryGetProperty("releases", out var releases) && releases.GetArrayLength() > 0)
            {
                var release = releases[0];
                if (release.TryGetProperty("id", out var relIdProp))
                {
                    releaseId = relIdProp.GetString();
                }

                if (release.TryGetProperty("title", out var relTitleProp))
                {
                    metadata.Album = relTitleProp.GetString();
                }

                if (release.TryGetProperty("date", out var dateProp))
                {
                    var dateStr = dateProp.GetString();
                    if (!string.IsNullOrWhiteSpace(dateStr) && dateStr.Length >= 4 && int.TryParse(dateStr.Substring(0, 4), out var year))
                    {
                        metadata.Year = year;
                    }
                }

                if (release.TryGetProperty("media", out var mediaList) && mediaList.GetArrayLength() > 0)
                {
                    var media = mediaList[0];
                    if (media.TryGetProperty("track-offset", out var trackOffsetProp))
                    {
                        metadata.TrackNumber = (uint)(trackOffsetProp.GetInt32() + 1);
                    }
                }
            }

            // Tags (Genre)
            if (bestMatch.TryGetProperty("tags", out var tags) && tags.GetArrayLength() > 0)
            {
                var tag = tags[0].GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    metadata.Genre = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tag);
                }
            }

            // Cover Art
            if (!string.IsNullOrEmpty(releaseId))
            {
                metadata.CoverArt = await DownloadCoverArtAsync(releaseId, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Honor cooperative cancellation instead of degrading to fallback metadata.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve full MusicBrainz metadata for '{Title}'. Using fallback.", source.Title);
        }

        return metadata;
    }

    private async Task<byte[]?> DownloadCoverArtAsync(string releaseId, CancellationToken cancellationToken)
    {
        try
        {
            var coverArtUrl = $"{_options.CoverArtBaseUrl.TrimEnd('/')}/{releaseId}/front";
            _logger.LogInformation("Fetching cover art from: {Url}", coverArtUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, coverArtUrl);
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch cover art for MusicBrainz release '{ReleaseId}'", releaseId);
        }

        return null;
    }

    private static (string? artist, string track) ExtractArtistAndTrack(string title, string uploader)
    {
        var cleanTitle = Regex.Replace(title, @"\s*\([^)]*?(official|lyric|video|audio|hd|4k|mv|music video)[^)]*?\)", "", RegexOptions.IgnoreCase);
        cleanTitle = Regex.Replace(cleanTitle, @"\s*\[[^\]]*?(official|lyric|video|audio|hd|4k|mv|music video)[^\]]*?\]", "", RegexOptions.IgnoreCase).Trim();

        var parts = cleanTitle.Split(new[] { " - ", " – ", " — " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return (parts[0].Trim(), parts[1].Trim());
        }

        var artist = uploader != "Unknown" && !string.IsNullOrWhiteSpace(uploader) ? uploader : null;
        return (artist, cleanTitle);
    }
}