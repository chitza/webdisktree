using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;

namespace WebDiskTree.Infrastructure.Scheduling;

/// <summary>
/// Keeps only the most recent <see cref="ScheduleRetentionOptions.MaxScansPerSchedule"/> finished scans for a
/// given schedule, deleting anything older along with its FileEntries/DirectoryPaths rows and blob file — the
/// same cleanup <see cref="WebDiskTree.Api.Controllers.ScansController.DeleteScan"/> does for a manual delete.
/// This is what keeps the DB from growing unbounded across repeated reruns of the same schedule.
/// </summary>
public class ScheduleRetentionService(
    IOptions<ScheduleRetentionOptions> options,
    ILogger<ScheduleRetentionService> logger)
{
    public async Task PruneAsync(WebDiskTreeDbContext dbContext, Guid scheduleId, CancellationToken cancellationToken)
    {
        var keep = Math.Max(1, options.Value.MaxScansPerSchedule);

        // Pending/Running scans are excluded: they haven't produced FileEntries/blob data yet and are either
        // about to be picked up or already in flight, so pruning must never race them. Pinned scans are excluded
        // too — pinning is an explicit "never auto-delete this one" and doesn't consume a slot in the kept count.
        // Ordered client-side: Sqlite can't ORDER BY a DateTimeOffset column (see ScansController.GetScans),
        // and this table is small enough per schedule that in-memory sorting is not a concern.
        var scans = await dbContext.Scans
            .Where(s => s.ScheduleId == scheduleId && !s.IsPinned
                && s.Status != ScanStatus.Pending && s.Status != ScanStatus.Running)
            .ToListAsync(cancellationToken);

        var toDelete = scans.OrderByDescending(s => s.CompletedAt ?? s.StartedAt).Skip(keep).ToList();
        if (toDelete.Count == 0)
        {
            return;
        }

        foreach (var scan in toDelete)
        {
            await dbContext.FileEntries.Where(f => f.ScanSeq == scan.SeqId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.DirectoryPaths.Where(d => d.ScanSeq == scan.SeqId).ExecuteDeleteAsync(cancellationToken);

            if (scan.BlobPath is not null && File.Exists(scan.BlobPath))
            {
                File.Delete(scan.BlobPath);
            }

            dbContext.Scans.Remove(scan);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Reclaims the pages just freed above — cheap, since the database runs incremental (not full) auto-vacuum.
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA incremental_vacuum;", cancellationToken);

        logger.LogInformation(
            "Pruned {Count} old scan(s) for schedule {ScheduleId}, keeping the {Keep} most recent",
            toDelete.Count, scheduleId, keep);
    }
}
