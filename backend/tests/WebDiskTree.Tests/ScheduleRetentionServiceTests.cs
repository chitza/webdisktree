using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Scheduling;

namespace WebDiskTree.Tests;

public class ScheduleRetentionServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebDiskTreeDbContext db;
    private readonly string blobDirectory;

    public ScheduleRetentionServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<WebDiskTreeDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();

        blobDirectory = Directory.CreateTempSubdirectory("webdisktree-retention-test-").FullName;
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
        Directory.Delete(blobDirectory, recursive: true);
    }

    private async Task<ScanEntity> AddScanAsync(
        Guid scheduleId, DateTimeOffset completedAt, ScanStatus status = ScanStatus.Completed, bool isPinned = false)
    {
        var blobPath = Path.Combine(blobDirectory, $"{Guid.NewGuid()}.json.gz");
        await File.WriteAllTextAsync(blobPath, "blob");

        var scan = new ScanEntity
        {
            Id = Guid.NewGuid(),
            RootPath = "/data",
            ScheduleId = scheduleId,
            Trigger = ScanTrigger.Scheduled,
            Status = status,
            IsPinned = isPinned,
            StartedAt = completedAt.AddMinutes(-1),
            CompletedAt = status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled ? completedAt : null,
            BlobPath = blobPath,
        };
        db.Scans.Add(scan);
        await db.SaveChangesAsync();

        db.FileEntries.Add(new FileEntryEntity { ScanSeq = scan.SeqId, ParentDirectoryId = 0, Name = "file.txt" });
        db.DirectoryPaths.Add(new DirectoryPathEntity { ScanSeq = scan.SeqId, Path = "/data" });
        await db.SaveChangesAsync();

        return scan;
    }

    private static ScheduleRetentionService CreateService(int maxScansPerSchedule) =>
        new(Options.Create(new ScheduleRetentionOptions { MaxScansPerSchedule = maxScansPerSchedule }),
            NullLogger<ScheduleRetentionService>.Instance);

    [Fact]
    public async Task KeepsOnlyTheMostRecentScansUpToTheConfiguredLimit()
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var oldest = await AddScanAsync(scheduleId, now.AddHours(-3));
        var older = await AddScanAsync(scheduleId, now.AddHours(-2));
        var newer = await AddScanAsync(scheduleId, now.AddHours(-1));
        var newest = await AddScanAsync(scheduleId, now);

        await CreateService(3).PruneAsync(db, scheduleId, CancellationToken.None);

        var remainingIds = await db.Scans.Select(s => s.Id).ToListAsync();
        Assert.Equal(3, remainingIds.Count);
        Assert.DoesNotContain(oldest.Id, remainingIds);
        Assert.Contains(older.Id, remainingIds);
        Assert.Contains(newer.Id, remainingIds);
        Assert.Contains(newest.Id, remainingIds);
    }

    [Fact]
    public async Task DeletesFileEntriesDirectoryPathsAndBlobForPrunedScans()
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var toPrune = await AddScanAsync(scheduleId, now.AddHours(-1));
        await AddScanAsync(scheduleId, now);

        await CreateService(1).PruneAsync(db, scheduleId, CancellationToken.None);

        Assert.False(await db.Scans.AnyAsync(s => s.Id == toPrune.Id));
        Assert.False(await db.FileEntries.AnyAsync(f => f.ScanSeq == toPrune.SeqId));
        Assert.False(await db.DirectoryPaths.AnyAsync(d => d.ScanSeq == toPrune.SeqId));
        Assert.False(File.Exists(toPrune.BlobPath));
    }

    [Fact]
    public async Task NeverPrunesPendingOrRunningScansRegardlessOfLimit()
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await AddScanAsync(scheduleId, now.AddHours(-2));
        await AddScanAsync(scheduleId, now.AddHours(-1));
        var running = await AddScanAsync(scheduleId, now, ScanStatus.Running);
        var pending = await AddScanAsync(scheduleId, now, ScanStatus.Pending);

        await CreateService(1).PruneAsync(db, scheduleId, CancellationToken.None);

        var remainingIds = await db.Scans.Select(s => s.Id).ToListAsync();
        Assert.Contains(running.Id, remainingIds);
        Assert.Contains(pending.Id, remainingIds);
    }

    [Fact]
    public async Task NeverPrunesPinnedScansAndDoesNotCountThemAgainstTheLimit()
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var pinned = await AddScanAsync(scheduleId, now.AddHours(-5), isPinned: true);
        var oldest = await AddScanAsync(scheduleId, now.AddHours(-3));
        var newest = await AddScanAsync(scheduleId, now);

        await CreateService(1).PruneAsync(db, scheduleId, CancellationToken.None);

        var remainingIds = await db.Scans.Select(s => s.Id).ToListAsync();
        Assert.Contains(pinned.Id, remainingIds);
        Assert.Contains(newest.Id, remainingIds);
        Assert.DoesNotContain(oldest.Id, remainingIds);
    }

    [Fact]
    public async Task DoesNotTouchScansFromOtherSchedules()
    {
        var scheduleA = Guid.NewGuid();
        var scheduleB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await AddScanAsync(scheduleA, now.AddHours(-2));
        await AddScanAsync(scheduleA, now.AddHours(-1));
        var otherSchedulesScan = await AddScanAsync(scheduleB, now.AddHours(-5));

        await CreateService(1).PruneAsync(db, scheduleA, CancellationToken.None);

        Assert.True(await db.Scans.AnyAsync(s => s.Id == otherSchedulesScan.Id));
    }
}
