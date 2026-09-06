using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Scanning;

namespace WebDiskTree.Api.Controllers;

public record ScanArchive(int Version, ScanSummaryDto Scan, DirectoryNode Tree, List<FlatFileRow> Files);

[ApiController]
[Route("api/scans")]
public class ScanArchivesController(
    WebDiskTreeDbContext db,
    TreeBlobSerializer serializer,
    FileEntryBulkWriter writer,
    IOptions<ScanStorageOptions> storage) : ControllerBase
{
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var scan = await db.Scans.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null) return NotFound();
        if (scan.Status != ScanStatus.Completed || scan.BlobPath is null || !System.IO.File.Exists(scan.BlobPath))
            return Conflict("Only completed scans with a saved tree can be exported.");

        var tree = await serializer.ReadAsync(scan.BlobPath, cancellationToken);
        if (tree is null) return Conflict("The saved tree is unavailable.");
        var files = await (from file in db.FileEntries.AsNoTracking()
                           join directory in db.DirectoryPaths on file.ParentDirectoryId equals directory.Id
                           where file.ScanId == id
                           select new FlatFileRow(directory.Path, file.Name, file.Extension,
                               file.SizeBytes, file.ModifiedUtc, file.IsDirectory)).ToListAsync(cancellationToken);
        var summary = new ScanSummaryDto(scan.Id, scan.RootPath, scan.Trigger, scan.Status,
            scan.StartedAt, scan.CompletedAt, scan.TotalBytes, scan.TotalFiles, scan.TotalDirs,
            scan.ErrorCount, scan.IsStale, scan.ErrorMessage, scan.IsPinned);
        var package = await ScanArchivePackage.WriteAsync(new ScanArchive(1, summary, tree, files), cancellationToken);
        var pathLabel = Regex.Replace(scan.RootPath, @"[^a-zA-Z0-9._-]+", "-").Trim('-', '.');
        if (pathLabel.Length == 0) pathLabel = "root";
        if (pathLabel.Length > 120) pathLabel = pathLabel[..120];
        var scanDate = (scan.StartedAt ?? scan.CompletedAt)?.UtcDateTime
            .ToString("yyyy-MM-dd_HH-mm-ss'Z'", CultureInfo.InvariantCulture) ?? "undated";
        return File(package, "application/gzip", $"webdisktree-{pathLabel}-{scanDate}.tar.gz");
    }

    [HttpPost("import")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> Import(CancellationToken cancellationToken)
    {
        ScanArchive? archive;
        try
        {
            archive = Request.ContentType?.Split(';')[0].Trim() == "application/json"
                ? await JsonSerializer.DeserializeAsync<ScanArchive>(Request.Body,
                    TreeBlobSerializer.ArchiveJsonOptions, cancellationToken)
                : await ScanArchivePackage.ReadAsync<ScanArchive>(Request.Body, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or EndOfStreamException)
        {
            return BadRequest("Invalid scan archive. Choose a WebDiskTree .tar.gz export or legacy JSON export.");
        }

        if (!IsValid(archive))
            return BadRequest("Invalid or unsupported scan export. Expected WebDiskTree format version 1.");

        var source = archive!.Scan;
        var id = Guid.NewGuid();
        var blobPath = Path.Combine(storage.Value.BlobDirectory, $"{id}.json.gz");
        var scan = new ScanEntity
        {
            Id = id, RootPath = source.RootPath, Trigger = ScanTrigger.Imported,
            Status = ScanStatus.Completed, StartedAt = source.StartedAt, CompletedAt = source.CompletedAt,
            TotalBytes = source.TotalBytes, TotalFiles = source.TotalFiles, TotalDirs = source.TotalDirs,
            ErrorCount = source.ErrorCount, IsStale = source.IsStale, ErrorMessage = source.ErrorMessage,
            BlobPath = blobPath, IsPinned = source.IsPinned,
        };
        try
        {
            // Publish the scan only once both the tree and all listing batches are stored.
            await serializer.WriteAsync(blobPath, archive.Tree, cancellationToken);
            await writer.WriteAsync(id, archive.Files, cancellationToken);
            db.Scans.Add(scan);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await db.FileEntries.Where(f => f.ScanId == id).ExecuteDeleteAsync(CancellationToken.None);
            await db.DirectoryPaths.Where(d => d.ScanId == id).ExecuteDeleteAsync(CancellationToken.None);
            System.IO.File.Delete(blobPath);
            throw;
        }

        return Created($"/api/scans/{id}", new ScanSummaryDto(id, scan.RootPath, scan.Trigger, scan.Status,
            scan.StartedAt, scan.CompletedAt, scan.TotalBytes, scan.TotalFiles, scan.TotalDirs,
            scan.ErrorCount, scan.IsStale, scan.ErrorMessage, scan.IsPinned));
    }

    private static bool IsValid(ScanArchive? archive)
    {
        if (archive is not { Version: 1, Scan: not null, Tree: not null, Files: not null }) return false;
        var scan = archive.Scan;
        if (scan.Status != ScanStatus.Completed || string.IsNullOrWhiteSpace(scan.RootPath)
            || scan.RootPath != archive.Tree.FullPath || scan.TotalBytes < 0 || scan.TotalFiles < 0
            || scan.TotalDirs < 0 || scan.ErrorCount < 0) return false;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<DirectoryNode>();
        pending.Push(archive.Tree);
        while (pending.TryPop(out var node))
        {
            if (node is null || string.IsNullOrWhiteSpace(node.FullPath) || node.Name is null
                || !paths.Add(node.FullPath) || node.SizeBytes < 0 || node.OtherFilesCount < 0
                || node.OtherFilesSizeBytes < 0 || node.Directories is null || node.Files is null
                || node.Files.Any(f => f is null || string.IsNullOrEmpty(f.Name) || f.SizeBytes < 0)) return false;
            foreach (var child in node.Directories) pending.Push(child);
        }
        return archive.Files.All(f => f is not null && paths.Contains(f.ParentPath)
            && !string.IsNullOrEmpty(f.Name) && f.SizeBytes >= 0);
    }
}
