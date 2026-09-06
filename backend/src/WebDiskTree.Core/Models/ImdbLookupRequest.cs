namespace WebDiskTree.Core.Models;

public record ImdbLookupRequest(string CacheKey, string Title, int? Year, MediaKind Kind);
