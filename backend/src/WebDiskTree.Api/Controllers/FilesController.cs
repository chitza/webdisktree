using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Media;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api")]
public class FilesController(
    WebDiskTreeDbContext dbContext,
    FileEntryBulkWriter bulkWriter,
    IPathSafetyValidator pathSafetyValidator,
    ImdbLookupQueue imdbLookupQueue) : ControllerBase
{
    [HttpGet("scans/{id:guid}/tree")]
    public async Task<IActionResult> GetTree(Guid id, CancellationToken cancellationToken)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null)
        {
            return NotFound();
        }

        if (scan.Status != ScanStatus.Completed || scan.BlobPath is null || !System.IO.File.Exists(scan.BlobPath))
        {
            return Conflict("Scan has no completed tree available yet.");
        }

        Response.Headers.ContentEncoding = "gzip";
        Response.Headers["X-Tree-Stale"] = scan.IsStale ? "true" : "false";
        return PhysicalFile(scan.BlobPath, "application/json");
    }

    [HttpGet("scans/{id:guid}/files")]
    public async Task<ActionResult<PagedResult<FileEntryDto>>> GetFiles(
        Guid id,
        [FromQuery] string path,
        [FromQuery] string sort = "size",
        [FromQuery] string dir = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest("path is required.");
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var scanSeq = await ResolveScanSeqAsync(id, cancellationToken);
        var directoryId = await dbContext.DirectoryPaths
            .Where(d => d.ScanSeq == scanSeq && d.Path == path)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (directoryId is null)
        {
            return Ok(new PagedResult<FileEntryDto>([], page, pageSize, 0));
        }

        var query = dbContext.FileEntries.Where(f => f.ScanSeq == scanSeq && f.ParentDirectoryId == directoryId);

        var descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(f => f.Name) : query.OrderBy(f => f.Name),
            "extension" => descending ? query.OrderByDescending(f => f.Extension) : query.OrderBy(f => f.Extension),
            "modified" => descending ? query.OrderByDescending(f => f.ModifiedUtc) : query.OrderBy(f => f.ModifiedUtc),
            _ => descending ? query.OrderByDescending(f => f.SizeBytes) : query.OrderBy(f => f.SizeBytes),
        };

        var totalCount = await query.LongCountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await AttachImdbInfoAsync(rows, cancellationToken);

        return Ok(new PagedResult<FileEntryDto>(items, page, pageSize, totalCount));
    }

    [HttpPost("scans/{id:guid}/imdb-lookup")]
    public async Task<ActionResult<TriggerImdbLookupResult>> TriggerImdbLookup(
        Guid id, TriggerImdbLookupRequest request, CancellationToken cancellationToken)
    {
        var scanSeq = await ResolveScanSeqAsync(id, cancellationToken);
        var directoryId = await dbContext.DirectoryPaths
            .Where(d => d.ScanSeq == scanSeq && d.Path == request.Path)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (directoryId is null)
        {
            return NotFound("Directory not found in this scan.");
        }

        var entries = await dbContext.FileEntries
            .Where(f => f.ScanSeq == scanSeq && f.ParentDirectoryId == directoryId)
            .Select(f => new { f.Name, f.IsDirectory })
            .ToListAsync(cancellationToken);

        var parsed = new Dictionary<string, (string Title, int? Year, MediaKind Kind)>();
        foreach (var entry in entries)
        {
            bool matched;
            string title;
            int? year;
            MediaKind kind;
            if (entry.IsDirectory)
            {
                matched = MediaTitleParser.TryParseDirectoryName(entry.Name, out title, out year, out kind);
            }
            else
            {
                matched = MediaTitleParser.TryParse(entry.Name, out title, out year, out kind);
            }

            if (matched)
            {
                parsed.TryAdd(MediaTitleParser.CacheKey(title, year), (title, year, kind));
            }
        }

        if (parsed.Count == 0)
        {
            return Ok(new TriggerImdbLookupResult(0, 0));
        }

        var cacheKeys = parsed.Keys.ToList();
        var existing = await dbContext.ImdbLookupCache
            .Where(c => cacheKeys.Contains(c.CacheKey))
            .ToDictionaryAsync(c => c.CacheKey, cancellationToken);

        var alreadyCached = 0;
        var now = DateTimeOffset.UtcNow;
        var toEnqueue = new List<ImdbLookupRequest>();

        foreach (var (cacheKey, (title, year, kind)) in parsed)
        {
            if (existing.TryGetValue(cacheKey, out var cacheEntry))
            {
                if (cacheEntry.Status is ImdbLookupStatus.Found or ImdbLookupStatus.NotFound)
                {
                    alreadyCached++;
                    continue;
                }

                cacheEntry.Status = ImdbLookupStatus.Pending;
                cacheEntry.LastAttemptAt = now;
            }
            else
            {
                dbContext.ImdbLookupCache.Add(new ImdbLookupCacheEntity
                {
                    CacheKey = cacheKey,
                    ParsedTitle = title,
                    Year = year,
                    Kind = kind,
                    Status = ImdbLookupStatus.Pending,
                    LastAttemptAt = now,
                });
            }

            toEnqueue.Add(new ImdbLookupRequest(cacheKey, title, year, kind));
        }

        // Persist the Pending rows before enqueueing: the background service reads them back by CacheKey
        // from its own DB context as soon as it dequeues, which can race ahead of this save otherwise.
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var lookupRequest in toEnqueue)
        {
            imdbLookupQueue.Enqueue(lookupRequest);
        }

        return Ok(new TriggerImdbLookupResult(toEnqueue.Count, alreadyCached));
    }

    [HttpGet("scans/{id:guid}/breakdown")]
    public async Task<ActionResult<IReadOnlyList<TypeBreakdownEntryDto>>> GetBreakdown(
        Guid id, [FromQuery] int top = 15, CancellationToken cancellationToken = default)
    {
        var scanSeq = await ResolveScanSeqAsync(id, cancellationToken);
        var raw = await dbContext.FileEntries
            .Where(f => f.ScanSeq == scanSeq && !f.IsDirectory)
            .GroupBy(f => f.Extension)
            .Select(g => new { Extension = g.Key, TotalSizeBytes = g.Sum(f => f.SizeBytes), FileCount = g.LongCount() })
            .ToListAsync(cancellationToken);

        var grouped = raw
            .OrderByDescending(e => e.TotalSizeBytes)
            .Select(e => new TypeBreakdownEntryDto(e.Extension ?? "(no extension)", e.TotalSizeBytes, e.FileCount))
            .ToList();

        if (grouped.Count <= top)
        {
            return Ok(grouped);
        }

        var head = grouped.Take(top).ToList();
        var tail = grouped.Skip(top).ToList();
        head.Add(new TypeBreakdownEntryDto("(other)", tail.Sum(e => e.TotalSizeBytes), tail.Sum(e => e.FileCount)));
        return Ok(head);
    }

    [HttpPost("files/delete")]
    public async Task<ActionResult<DeleteFilesResult>> DeleteFiles(DeleteFilesRequest request, CancellationToken cancellationToken)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == request.ScanId, cancellationToken);
        if (scan is null)
        {
            return NotFound("Scan not found.");
        }

        if (scan.Trigger == ScanTrigger.Imported)
        {
            return Conflict("Imported scans are read-only.");
        }

        var deleted = new List<string>();
        var failed = new List<DeleteFailureDto>();

        foreach (var requestedPath in request.Paths)
        {
            if (!pathSafetyValidator.TryValidateForDelete(scan.RootPath, requestedPath, out var canonicalPath, out var error))
            {
                failed.Add(new DeleteFailureDto(requestedPath, error ?? "Path rejected."));
                continue;
            }

            try
            {
                var isDirectory = Directory.Exists(canonicalPath);
                if (isDirectory)
                {
                    Directory.Delete(canonicalPath, recursive: true);
                }
                else
                {
                    System.IO.File.Delete(canonicalPath);
                }

                await bulkWriter.DeleteTreeAsync(scan.SeqId, canonicalPath, isDirectory, cancellationToken);
                deleted.Add(canonicalPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(new DeleteFailureDto(requestedPath, ex.Message));
            }
        }

        if (deleted.Count > 0)
        {
            scan.IsStale = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new DeleteFilesResult(deleted, failed));
    }

    // Public endpoints identify scans by Guid; storage keys FileEntries/DirectoryPaths off the much smaller
    // SeqId instead (see ScanEntity.SeqId). A nonexistent scan resolves to 0, which never matches a real row,
    // so callers naturally see empty results without needing a separate not-found branch here.
    private async Task<int> ResolveScanSeqAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Scans.Where(s => s.Id == id).Select(s => s.SeqId).FirstOrDefaultAsync(cancellationToken);

    private async Task<List<FileEntryDto>> AttachImdbInfoAsync(List<FileEntryEntity> rows, CancellationToken cancellationToken)
    {
        var parsedByRow = new Dictionary<FileEntryEntity, (string Title, int? Year)>();
        var cacheKeys = new HashSet<string>();
        foreach (var row in rows)
        {
            var matched = row.IsDirectory
                ? MediaTitleParser.TryParseDirectoryName(row.Name, out var title, out var year, out _)
                : MediaTitleParser.TryParse(row.Name, out title, out year, out _);

            if (matched)
            {
                parsedByRow[row] = (title, year);
                cacheKeys.Add(MediaTitleParser.CacheKey(title, year));
            }
        }

        var cacheByKey = cacheKeys.Count == 0
            ? new Dictionary<string, ImdbLookupCacheEntity>()
            : await dbContext.ImdbLookupCache
                .Where(c => cacheKeys.Contains(c.CacheKey))
                .ToDictionaryAsync(c => c.CacheKey, cancellationToken);

        return rows.Select(row =>
        {
            if (!parsedByRow.TryGetValue(row, out var parsed))
            {
                return new FileEntryDto(row.Name, row.Extension, row.SizeBytes, row.ModifiedUtc, row.IsDirectory);
            }

            var cacheEntry = cacheByKey.GetValueOrDefault(MediaTitleParser.CacheKey(parsed.Title, parsed.Year));
            var imdbUrl = cacheEntry?.Status == ImdbLookupStatus.Found ? $"https://www.imdb.com/title/{cacheEntry.ImdbId}/" : null;

            return new FileEntryDto(
                row.Name, row.Extension, row.SizeBytes, row.ModifiedUtc, row.IsDirectory,
                parsed.Title, imdbUrl, cacheEntry?.Status);
        }).ToList();
    }
}
