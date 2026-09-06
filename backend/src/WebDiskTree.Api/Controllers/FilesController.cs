using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api")]
public class FilesController(
    WebDiskTreeDbContext dbContext,
    FileEntryBulkWriter bulkWriter,
    IPathSafetyValidator pathSafetyValidator) : ControllerBase
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

        var directoryId = await dbContext.DirectoryPaths
            .Where(d => d.ScanId == id && d.Path == path)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (directoryId is null)
        {
            return Ok(new PagedResult<FileEntryDto>([], page, pageSize, 0));
        }

        var query = dbContext.FileEntries.Where(f => f.ScanId == id && f.ParentDirectoryId == directoryId);

        var descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(f => f.Name) : query.OrderBy(f => f.Name),
            "extension" => descending ? query.OrderByDescending(f => f.Extension) : query.OrderBy(f => f.Extension),
            "modified" => descending ? query.OrderByDescending(f => f.ModifiedUtc) : query.OrderBy(f => f.ModifiedUtc),
            _ => descending ? query.OrderByDescending(f => f.SizeBytes) : query.OrderBy(f => f.SizeBytes),
        };

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FileEntryDto(f.Name, f.Extension, f.SizeBytes, f.ModifiedUtc, f.IsDirectory))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<FileEntryDto>(items, page, pageSize, totalCount));
    }

    [HttpGet("scans/{id:guid}/breakdown")]
    public async Task<ActionResult<IReadOnlyList<TypeBreakdownEntryDto>>> GetBreakdown(
        Guid id, [FromQuery] int top = 15, CancellationToken cancellationToken = default)
    {
        var raw = await dbContext.FileEntries
            .Where(f => f.ScanId == id && !f.IsDirectory)
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

                await bulkWriter.DeleteTreeAsync(request.ScanId, canonicalPath, isDirectory, cancellationToken);
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
}
