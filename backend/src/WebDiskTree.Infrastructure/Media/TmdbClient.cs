using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Media;

/// <summary>Resolves a parsed title (+ year) to an IMDB id via TMDb's free API: a title search followed by
/// an external-ids lookup on the top match. Throws when no API key is configured (the caller maps this to
/// a retryable Failed status) rather than returning null, which must stay reserved for a genuine "TMDb has
/// no match" result — the two aren't the same and shouldn't be cached as if they were.</summary>
public class TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
{
    private bool _warnedMissingApiKey;

    public async Task<string?> FindImdbIdAsync(string title, int? year, MediaKind kind, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (!_warnedMissingApiKey)
            {
                logger.LogWarning("Tmdb:ApiKey is not configured; IMDB lookups will not run");
                _warnedMissingApiKey = true;
            }
            throw new InvalidOperationException("Tmdb:ApiKey is not configured.");
        }

        // MediaKind is only a guess when the name carried no season/episode marker (e.g. a plain
        // "Le.bureau.des.légendes" folder defaults to Movie) — searching only that guessed endpoint
        // would silently miss a real match, so the other media type is always tried as a fallback.
        var primaryPath = kind == MediaKind.Series ? "tv" : "movie";
        var secondaryPath = primaryPath == "movie" ? "tv" : "movie";

        return await SearchAsync(apiKey, primaryPath, title, year, cancellationToken)
            ?? await SearchAsync(apiKey, secondaryPath, title, null, cancellationToken);
    }

    private async Task<string?> SearchAsync(string apiKey, string mediaPath, string title, int? year, CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var yearParam = mediaPath == "tv" ? "first_air_date_year" : "year";
        var searchUrl = $"{baseUrl}/search/{mediaPath}?api_key={Uri.EscapeDataString(apiKey)}&query={Uri.EscapeDataString(title)}"
            + (year is not null ? $"&{yearParam}={year}" : "");

        var searchResult = await httpClient.GetFromJsonAsync<TmdbSearchResponse>(searchUrl, cancellationToken);
        var match = searchResult?.Results?.FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        var externalIdsUrl = $"{baseUrl}/{mediaPath}/{match.Id}/external_ids?api_key={Uri.EscapeDataString(apiKey)}";
        var externalIds = await httpClient.GetFromJsonAsync<TmdbExternalIdsResponse>(externalIdsUrl, cancellationToken);
        return string.IsNullOrWhiteSpace(externalIds?.ImdbId) ? null : externalIds.ImdbId;
    }

    private class TmdbSearchResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbSearchResult>? Results { get; set; }
    }

    private class TmdbSearchResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private class TmdbExternalIdsResponse
    {
        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; set; }
    }
}
