using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Infrastructure.Scanning;

/// <summary>
/// Dequeues scan requests (manual or scheduled — both paths share this one pipeline) and runs them one at a
/// time: walks the tree, writes the gzip blob, bulk-inserts flat rows, and updates the Scans row throughout.
/// </summary>
public class ScanBackgroundService(
    ScanQueue queue,
    ScanCancellationRegistry cancellationRegistry,
    IScanEngine scanEngine,
    IScanProgressReporter progressReporter,
    TreeBlobSerializer blobSerializer,
    IServiceScopeFactory scopeFactory,
    IOptions<ScanStorageOptions> storageOptions,
    ILogger<ScanBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunScanAsync(request, stoppingToken);
        }
    }

    private async Task RunScanAsync(ScanJobRequest request, CancellationToken hostStoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebDiskTreeDbContext>();
        var bulkWriter = scope.ServiceProvider.GetRequiredService<FileEntryBulkWriter>();

        var cts = cancellationRegistry.Register(request.ScanId);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, hostStoppingToken);

        var scanEntity = await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == request.ScanId, hostStoppingToken);
        if (scanEntity is null)
        {
            logger.LogWarning("Scan {ScanId} not found in database; skipping", request.ScanId);
            cancellationRegistry.Remove(request.ScanId);
            return;
        }

        if (scanEntity.Status == ScanStatus.Cancelled)
        {
            // Cancelled by the API while still Pending in the queue (never reached Running) — nothing to do.
            cancellationRegistry.Remove(request.ScanId);
            return;
        }

        scanEntity.Status = ScanStatus.Running;
        scanEntity.StartedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(hostStoppingToken);

        var job = new ScanJob { Id = request.ScanId, RootPath = request.RootPath, Trigger = request.Trigger };

        try
        {
            var result = await scanEngine.ScanAsync(job, progressReporter, linkedCts.Token);

            var blobPath = Path.Combine(storageOptions.Value.BlobDirectory, $"{request.ScanId}.json.gz");
            await blobSerializer.WriteAsync(blobPath, result.Root, linkedCts.Token);
            await bulkWriter.WriteAsync(scanEntity.SeqId, result.FlatRows, linkedCts.Token);

            scanEntity.Status = ScanStatus.Completed;
            scanEntity.CompletedAt = DateTimeOffset.UtcNow;
            scanEntity.TotalBytes = job.TotalBytes;
            scanEntity.TotalFiles = job.TotalFiles;
            scanEntity.TotalDirs = job.TotalDirs;
            scanEntity.ErrorCount = job.ErrorCount;
            scanEntity.BlobPath = blobPath;
            await dbContext.SaveChangesAsync(hostStoppingToken);

            progressReporter.ReportCompleted(request.ScanId);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            scanEntity.Status = ScanStatus.Cancelled;
            scanEntity.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(hostStoppingToken);
            progressReporter.ReportCancelled(request.ScanId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan {ScanId} of {RootPath} failed", request.ScanId, request.RootPath);
            scanEntity.Status = ScanStatus.Failed;
            scanEntity.CompletedAt = DateTimeOffset.UtcNow;
            scanEntity.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(hostStoppingToken);
            progressReporter.ReportFailed(request.ScanId, ex.Message);
        }
        finally
        {
            cancellationRegistry.Remove(request.ScanId);
        }
    }

}

public class ScanStorageOptions
{
    public required string BlobDirectory { get; set; }
}
