using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Scanning;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api/scans")]
public class ScansController(
    WebDiskTreeDbContext dbContext,
    ScanQueue queue,
    ScanCancellationRegistry cancellationRegistry,
    AllowedRootsService allowedRoots) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScanSummaryDto>> CreateScan(CreateScanRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            return BadRequest("RootPath is required.");
        }

        if (!allowedRoots.IsAllowed(request.RootPath))
        {
            return BadRequest("RootPath is not under any configured allowed root.");
        }

        if (!Directory.Exists(request.RootPath))
        {
            return BadRequest("RootPath does not exist or is not accessible.");
        }

        var entity = new ScanEntity
        {
            Id = Guid.NewGuid(),
            RootPath = Path.GetFullPath(request.RootPath),
            Trigger = ScanTrigger.Manual,
            Status = ScanStatus.Pending,
        };

        dbContext.Scans.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        queue.Enqueue(new ScanJobRequest(entity.Id, entity.RootPath, ScanTrigger.Manual));

        return CreatedAtAction(nameof(GetScan), new { id = entity.Id }, ToDto(entity));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScanSummaryDto>>> GetScans(CancellationToken cancellationToken)
    {
        // Ordered client-side: Sqlite can't ORDER BY a DateTimeOffset column, and the Scans table is small
        // (one row per scan run, not per file) so this never needs to scale like the FileEntries queries do.
        var scans = await dbContext.Scans.ToListAsync(cancellationToken);
        return Ok(scans.OrderByDescending(s => s.StartedAt).Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScanSummaryDto>> GetScan(Guid id, CancellationToken cancellationToken)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return scan is null ? NotFound() : Ok(ToDto(scan));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelScan(Guid id, CancellationToken cancellationToken)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null)
        {
            return NotFound();
        }

        if (scan.Status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled)
        {
            return Conflict("Scan has already finished.");
        }

        if (!cancellationRegistry.TryCancel(id))
        {
            // Not running yet (still Pending in the queue) — mark cancelled directly; the background
            // service checks scan status before starting work and will skip it.
            scan.Status = ScanStatus.Cancelled;
            scan.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/pin")]
    public async Task<ActionResult<ScanSummaryDto>> SetPinned(
        Guid id, SetScanPinnedRequest request, CancellationToken cancellationToken)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null) return NotFound();

        scan.IsPinned = request.IsPinned;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(scan));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteScan(Guid id, CancellationToken cancellationToken, [FromQuery] bool confirmPinned = false)
    {
        var scan = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null)
        {
            return NotFound();
        }

        if (scan.Status is ScanStatus.Pending or ScanStatus.Running)
        {
            return Conflict("Scan is still in progress. Cancel it before deleting.");
        }

        if (scan.IsPinned && !confirmPinned)
        {
            return Conflict("This scan is pinned. Confirm deletion of the pinned scan.");
        }

        await dbContext.FileEntries.Where(f => f.ScanId == id).ExecuteDeleteAsync(cancellationToken);
        await dbContext.DirectoryPaths.Where(d => d.ScanId == id).ExecuteDeleteAsync(cancellationToken);

        if (scan.BlobPath is not null && System.IO.File.Exists(scan.BlobPath))
        {
            System.IO.File.Delete(scan.BlobPath);
        }

        dbContext.Scans.Remove(scan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static ScanSummaryDto ToDto(ScanEntity s) => new(
        s.Id, s.RootPath, s.Trigger, s.Status, s.StartedAt, s.CompletedAt,
        s.TotalBytes, s.TotalFiles, s.TotalDirs, s.ErrorCount, s.IsStale, s.ErrorMessage, s.IsPinned);
}
