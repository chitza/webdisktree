using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Controllers;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Tests;

public class ScanPinTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebDiskTreeDbContext db;
    private readonly ScansController controller;

    public ScanPinTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<WebDiskTreeDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();
        controller = new(db, null!, null!, null!);
    }

    [Fact]
    public async Task PinAndUnpinPersistAndAppearInSummaries()
    {
        var scan = new ScanEntity { Id = Guid.NewGuid(), RootPath = "/", Status = ScanStatus.Completed };
        db.Scans.Add(scan);
        await db.SaveChangesAsync();
        Assert.False(scan.IsPinned);

        foreach (var pinned in new[] { true, false })
        {
            var result = await controller.SetPinned(scan.Id, new(pinned), default);
            Assert.Equal(pinned, Assert.IsType<ScanSummaryDto>(Assert.IsType<OkObjectResult>(result.Result).Value).IsPinned);
            db.ChangeTracker.Clear();
            Assert.Equal(pinned, (await db.Scans.SingleAsync()).IsPinned);
            var list = await controller.GetScans(default);
            Assert.Equal(pinned, Assert.Single(Assert.IsType<List<ScanSummaryDto>>(Assert.IsType<OkObjectResult>(list.Result).Value)).IsPinned);
        }
        Assert.IsType<NotFoundResult>((await controller.SetPinned(Guid.NewGuid(), new(true), default)).Result);
    }

    [Fact]
    public async Task PinnedScanRequiresExplicitConfirmationBeforeDeletingAnyData()
    {
        var blob = Path.GetTempFileName();
        try
        {
            var scan = new ScanEntity { Id = Guid.NewGuid(), RootPath = "/", Status = ScanStatus.Completed, IsPinned = true, BlobPath = blob };
            db.Scans.Add(scan);
            db.DirectoryPaths.Add(new() { ScanId = scan.Id, Path = "/" });
            await db.SaveChangesAsync();
            Assert.IsType<ConflictObjectResult>(await controller.DeleteScan(scan.Id, default));
            Assert.True(File.Exists(blob));
            Assert.Single(db.Scans);
            Assert.Single(db.DirectoryPaths);

            Assert.IsType<NoContentResult>(await controller.DeleteScan(scan.Id, default, true));
            Assert.False(File.Exists(blob));
            Assert.Empty(db.Scans);
            Assert.Empty(db.DirectoryPaths);
        }
        finally { File.Delete(blob); }
    }

    [Theory]
    [InlineData(ScanStatus.Completed, false)]
    [InlineData(ScanStatus.Running, true)]
    [InlineData(ScanStatus.Pending, true)]
    public async Task ConfirmationDoesNotBypassInProgressGuard(ScanStatus status, bool pinned)
    {
        var scan = new ScanEntity { Id = Guid.NewGuid(), RootPath = "/", Status = status, IsPinned = pinned };
        db.Scans.Add(scan);
        await db.SaveChangesAsync();
        var result = await controller.DeleteScan(scan.Id, default, pinned);
        if (status == ScanStatus.Completed) Assert.IsType<NoContentResult>(result);
        else
        {
            Assert.IsType<ConflictObjectResult>(result);
            Assert.Single(db.Scans);
        }
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }
}
