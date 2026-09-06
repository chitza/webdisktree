using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Formats.Tar;
using System.IO.Compression;
using WebDiskTree.Api.Controllers;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Scanning;

namespace WebDiskTree.Tests;

public class ScanArchiveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebDiskTreeDbContext db;
    private readonly ScanArchivesController controller;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ScanArchiveTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<WebDiskTreeDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        controller = new(db, new TreeBlobSerializer(), new FileEntryBulkWriter(db),
            Options.Create(new ScanStorageOptions { BlobDirectory = directory }));
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext() };
    }

    private static ScanArchive Archive() => new(1,
        new ScanSummaryDto(Guid.NewGuid(), "/offline", ScanTrigger.Manual, ScanStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 42, 2, 1, 0, true, null),
        new DirectoryNode
        {
            Name = "offline", FullPath = "/offline", SizeBytes = 42,
            Directories = [new DirectoryNode { Name = "child", FullPath = "/offline/child", SizeBytes = 42,
                Files = [new FileEntry { Name = "large.txt", SizeBytes = 40 }],
                OtherFilesCount = 1, OtherFilesSizeBytes = 2 }],
        },
        [new FlatFileRow("/offline", "child", null, 42, DateTimeOffset.UtcNow, true),
         new FlatFileRow("/offline/child", "large.txt", ".txt", 40, DateTimeOffset.UtcNow, false),
         new FlatFileRow("/offline/child", "small.txt", ".txt", 2, DateTimeOffset.UtcNow, false)]);

    private void SetBody(string json)
    {
        controller.Request.ContentType = "application/json";
        controller.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public async Task RoundTripPreservesNestedTreeAndAllFilesWithNewIdentity()
    {
        var archive = Archive();
        SetBody(JsonSerializer.Serialize(archive, JsonOptions));
        var result = Assert.IsType<CreatedResult>(await controller.Import(default));
        var summary = Assert.IsType<ScanSummaryDto>(result.Value);
        Assert.NotEqual(archive.Scan.Id, summary.Id);
        Assert.Equal(ScanTrigger.Imported, summary.Trigger);
        Assert.True(summary.IsStale);
        Assert.Equal(archive.Scan.StartedAt, summary.StartedAt);
        var exported = Assert.IsType<FileStreamResult>(await controller.Export(summary.Id, default));
        await using var package = exported.FileStream;
        var restored = await ScanArchivePackage.ReadAsync<ScanArchive>(package, default);
        Assert.NotNull(restored);
        Assert.Equal("large.txt", Assert.Single(Assert.Single(restored.Tree.Directories).Files).Name);
        Assert.Equal(1, restored.Tree.Directories[0].OtherFilesCount);
        Assert.Equal(3, restored.Files.Count);
        Assert.Contains(restored.Files, f => f.Name == "small.txt" && f.SizeBytes == 2);
        SetBody(JsonSerializer.Serialize(restored, JsonOptions));
        Assert.IsType<CreatedResult>(await controller.Import(default));
        Assert.Equal(2, await db.Scans.CountAsync());
    }

    [Fact]
    public async Task DeepTreeSurvivesStorageHttpExportAndReimport()
    {
        var archive = Archive();
        var leaf = archive.Tree.Directories[0];
        for (var i = 0; i < 100; i++)
        {
            var child = new DirectoryNode { Name = $"level{i}", FullPath = $"{leaf.FullPath}/level{i}" };
            leaf.Directories.Add(child);
            leaf = child;
        }
        leaf.Files.Add(new FileEntry { Name = "deep.txt", SizeBytes = 1 });
        archive.Files.Add(new FlatFileRow(leaf.FullPath, "deep.txt", ".txt", 1, DateTimeOffset.UtcNow, false));
        SetBody(JsonSerializer.Serialize(archive, TreeBlobSerializer.ArchiveJsonOptions));
        var imported = Assert.IsType<CreatedResult>(await controller.Import(default));
        var summary = Assert.IsType<ScanSummaryDto>(imported.Value);
        var exported = await controller.Export(summary.Id, default);

        // Execute the actual MVC result: inspecting Value alone misses formatter depth limits.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        controller.HttpContext.RequestServices = provider;
        using var response = new MemoryStream();
        controller.Response.Body = response;
        await exported.ExecuteResultAsync(controller.ControllerContext);
        Assert.Contains("webdisktree-offline-", controller.Response.Headers.ContentDisposition.ToString());
        Assert.Contains(".tar.gz", controller.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("application/gzip", controller.Response.ContentType);
        response.Position = 0;
        var restored = await ScanArchivePackage.ReadAsync<ScanArchive>(response, default);
        Assert.NotNull(restored);
        var restoredLeaf = restored.Tree.Directories[0];
        for (var i = 0; i < 100; i++) restoredLeaf = Assert.Single(restoredLeaf.Directories);
        Assert.Equal("deep.txt", Assert.Single(restoredLeaf.Files).Name);
        Assert.Equal(leaf.FullPath, restoredLeaf.FullPath);
        response.Position = 0;
        controller.Request.Body = response;
        controller.Request.ContentType = "application/gzip";
        Assert.IsType<CreatedResult>(await controller.Import(default));
        Assert.Equal(2, await db.Scans.CountAsync());
    }

    [Theory]
    [InlineData("/hostfs/home", "hostfs-home")]
    [InlineData("/", "root")]
    [InlineData("C:\\Users\\Test Name", "C-Users-Test-Name")]
    public async Task DownloadNameUsesSafePathAndOriginalScanDate(string path, string expected)
    {
        var archive = Archive();
        archive = archive with
        {
            Scan = archive.Scan with { RootPath = path, StartedAt = DateTimeOffset.Parse("2026-09-06T09:24:07+03:00") },
            Tree = new DirectoryNode { Name = path, FullPath = path }, Files = [],
        };
        SetBody(JsonSerializer.Serialize(archive, TreeBlobSerializer.ArchiveJsonOptions));
        var imported = Assert.IsType<CreatedResult>(await controller.Import(default));
        var summary = Assert.IsType<ScanSummaryDto>(imported.Value);
        var result = Assert.IsType<FileStreamResult>(await controller.Export(summary.Id, default));
        await using var package = result.FileStream;
        Assert.Equal($"webdisktree-{expected}-2026-09-06_06-24-07Z.tar.gz", result.FileDownloadName);
        using var gzip = new GZipStream(package, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var entry = await tar.GetNextEntryAsync();
        Assert.NotNull(entry);
        Assert.Equal("scan.json", entry.Name);
        Assert.Equal(TarEntryType.RegularFile, entry.EntryType);
        Assert.Null(await tar.GetNextEntryAsync());
    }

    [Fact]
    public async Task RejectsInvalidCompressedPackageWithoutPersistingScan()
    {
        controller.Request.ContentType = "application/gzip";
        controller.Request.Body = new MemoryStream("not gzip"u8.ToArray());
        Assert.IsType<BadRequestObjectResult>(await controller.Import(default));
        Assert.Empty(db.Scans);
    }

    [Fact]
    public async Task RejectsUnexpectedTarEntryWithoutExtractingIt()
    {
        using var package = new MemoryStream();
        using (var gzip = new GZipStream(package, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gzip))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "../scan.json")
            {
                DataStream = new MemoryStream("{}"u8.ToArray()),
            });
        }
        package.Position = 0;
        controller.Request.ContentType = "application/gzip";
        controller.Request.Body = package;
        Assert.IsType<BadRequestObjectResult>(await controller.Import(default));
        Assert.Empty(db.Scans);
    }

    [Fact]
    public async Task ImportedScanCannotDeleteLocalFiles()
    {
        SetBody(JsonSerializer.Serialize(Archive(), JsonOptions));
        var result = Assert.IsType<CreatedResult>(await controller.Import(default));
        var summary = Assert.IsType<ScanSummaryDto>(result.Value);
        // A null validator ensures rejection happens before any filesystem validation or deletion.
        var files = new FilesController(db, new FileEntryBulkWriter(db), null!);
        var deletion = await files.DeleteFiles(new DeleteFilesRequest(summary.Id, ["/offline/child"]), default);
        Assert.IsType<ConflictObjectResult>(deletion.Result);
        Assert.Equal(3, await db.FileEntries.CountAsync());
    }

    [Fact]
    public async Task ExportRejectsUnfinishedScansAndMissingIds()
    {
        Assert.IsType<NotFoundResult>(await controller.Export(Guid.NewGuid(), default));
        var scan = new WebDiskTree.Infrastructure.Data.Entities.ScanEntity
        {
            Id = Guid.NewGuid(), RootPath = "/offline", Status = ScanStatus.Running,
        };
        db.Scans.Add(scan);
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await controller.Export(scan.Id, default));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"version\":99}")]
    public async Task InvalidImportDoesNotPersistAnything(string json)
    {
        SetBody(json);
        Assert.IsType<BadRequestObjectResult>(await controller.Import(default));
        Assert.Empty(db.Scans);
        Assert.Empty(db.FileEntries);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task RejectsRowsOutsideTree()
    {
        var archive = Archive();
        archive.Files.Add(new("/elsewhere", "file", null, 1, DateTimeOffset.UtcNow, false));
        SetBody(JsonSerializer.Serialize(archive, JsonOptions));
        Assert.IsType<BadRequestObjectResult>(await controller.Import(default));
        Assert.Empty(db.FileEntries);
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
