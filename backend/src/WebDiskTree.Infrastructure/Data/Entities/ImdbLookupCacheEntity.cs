using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Data.Entities;

/// <summary>
/// Caches the result of resolving a parsed title (+ year) to an IMDB id, keyed by
/// <see cref="MediaTitleParser.CacheKey"/> so the same title is never looked up twice, even across
/// unrelated scans or rescans of the same content.
/// </summary>
public class ImdbLookupCacheEntity
{
    public long Id { get; set; }
    public required string CacheKey { get; set; }
    public required string ParsedTitle { get; set; }
    public int? Year { get; set; }
    public MediaKind Kind { get; set; }
    public string? ImdbId { get; set; }
    public ImdbLookupStatus Status { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
