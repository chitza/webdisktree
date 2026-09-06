using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Api.Controllers;

public record ImdbLookupCacheArchive(int Version, List<ImdbLookupCacheRow> Entries);

/// <summary>Backs up/restores the IMDB lookup cache (title/year -> IMDB id), independent of any scan, so a
/// cache built up on one instance can be moved to another without re-running lookups against IMDB.</summary>
[ApiController]
[Route("api/imdb-lookup-cache")]
public class ImdbLookupCacheArchivesController(WebDiskTreeDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ImdbLookupCacheSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var count = await db.ImdbLookupCache.CountAsync(cancellationToken);
        return Ok(new ImdbLookupCacheSummaryDto(count));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var entries = await db.ImdbLookupCache.AsNoTracking()
            .Select(c => new ImdbLookupCacheRow(c.CacheKey, c.ParsedTitle, c.Year, c.Kind, c.ImdbId, c.Status, c.LastAttemptAt))
            .ToListAsync(cancellationToken);

        var package = await ScanArchivePackage.WriteAsync(new ImdbLookupCacheArchive(1, entries), cancellationToken);
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss'Z'", CultureInfo.InvariantCulture);
        return File(package, "application/gzip", $"webdisktree-imdb-lookup-cache-{date}.tar.gz");
    }

    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImdbLookupCacheImportResult>> Import(CancellationToken cancellationToken)
    {
        ImdbLookupCacheArchive? archive;
        try
        {
            archive = Request.ContentType?.Split(';')[0].Trim() == "application/json"
                ? await JsonSerializer.DeserializeAsync<ImdbLookupCacheArchive>(Request.Body,
                    TreeBlobSerializer.ArchiveJsonOptions, cancellationToken)
                : await ScanArchivePackage.ReadAsync<ImdbLookupCacheArchive>(Request.Body, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or EndOfStreamException)
        {
            return BadRequest("Invalid IMDB lookup cache archive. Choose a WebDiskTree .tar.gz export or legacy JSON export.");
        }

        if (!IsValid(archive))
            return BadRequest("Invalid or unsupported IMDB lookup cache export. Expected WebDiskTree format version 1.");

        // Upserts by CacheKey instead of replacing the table wholesale: restoring a backup onto a database
        // that already has its own cached lookups should fill gaps and refresh matching entries, not discard
        // local entries the backup doesn't know about.
        var cacheKeys = archive!.Entries.Select(e => e.CacheKey).ToList();
        var existing = await db.ImdbLookupCache
            .Where(c => cacheKeys.Contains(c.CacheKey))
            .ToDictionaryAsync(c => c.CacheKey, cancellationToken);

        var added = 0;
        var updated = 0;
        foreach (var row in archive.Entries)
        {
            if (existing.TryGetValue(row.CacheKey, out var entity))
            {
                entity.ParsedTitle = row.ParsedTitle;
                entity.Year = row.Year;
                entity.Kind = row.Kind;
                entity.ImdbId = row.ImdbId;
                entity.Status = row.Status;
                entity.LastAttemptAt = row.LastAttemptAt;
                updated++;
            }
            else
            {
                db.ImdbLookupCache.Add(new ImdbLookupCacheEntity
                {
                    CacheKey = row.CacheKey, ParsedTitle = row.ParsedTitle, Year = row.Year,
                    Kind = row.Kind, ImdbId = row.ImdbId, Status = row.Status, LastAttemptAt = row.LastAttemptAt,
                });
                added++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ImdbLookupCacheImportResult(added, updated));
    }

    private static bool IsValid(ImdbLookupCacheArchive? archive)
    {
        if (archive is not { Version: 1, Entries: not null }) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return archive.Entries.All(e => e is not null && !string.IsNullOrWhiteSpace(e.CacheKey)
            && !string.IsNullOrWhiteSpace(e.ParsedTitle) && e.Year is null or >= 0
            && Enum.IsDefined(e.Kind) && Enum.IsDefined(e.Status) && seen.Add(e.CacheKey));
    }
}
