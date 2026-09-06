using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Controllers;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Tests;

public class ImdbLookupCacheArchiveTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebDiskTreeDbContext db;
    private readonly ImdbLookupCacheArchivesController controller;

    public ImdbLookupCacheArchiveTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<WebDiskTreeDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        controller = new(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    }

    private static ImdbLookupCacheEntity Entry(string cacheKey, string title, string? imdbId = "tt0111161") => new()
    {
        CacheKey = cacheKey, ParsedTitle = title, Year = 1994, Kind = MediaKind.Movie,
        ImdbId = imdbId, Status = ImdbLookupStatus.Found, LastAttemptAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task RoundTripPreservesAllEntries()
    {
        db.ImdbLookupCache.Add(Entry("the shawshank redemption|1994", "The Shawshank Redemption"));
        db.ImdbLookupCache.Add(Entry("planet earth ii|", "Planet Earth II", imdbId: null));
        await db.SaveChangesAsync();

        var exported = Assert.IsType<FileStreamResult>(await controller.Export(default));
        var archive = await ScanArchivePackage.ReadAsync<ImdbLookupCacheArchive>(exported.FileStream, default);
        Assert.Equal(2, archive!.Entries.Count);

        await db.ImdbLookupCache.ExecuteDeleteAsync();
        Assert.Empty(db.ImdbLookupCache);

        var package = await ScanArchivePackage.WriteAsync(archive, default);
        controller.ControllerContext.HttpContext.Request.Body = package;
        var result = Assert.IsType<OkObjectResult>((await controller.Import(default)).Result);
        var summary = Assert.IsType<ImdbLookupCacheImportResult>(result.Value);
        Assert.Equal(2, summary.Added);
        Assert.Equal(0, summary.Updated);

        db.ChangeTracker.Clear();
        var restored = await db.ImdbLookupCache.SingleAsync(c => c.CacheKey == "the shawshank redemption|1994");
        Assert.Equal("The Shawshank Redemption", restored.ParsedTitle);
        Assert.Equal("tt0111161", restored.ImdbId);
    }

    [Fact]
    public async Task ImportUpsertsByCacheKeyInsteadOfReplacingWholesale()
    {
        db.ImdbLookupCache.Add(Entry("existing|2000", "Stale Title", imdbId: "tt0000001"));
        db.ImdbLookupCache.Add(Entry("untouched|2001", "Untouched"));
        await db.SaveChangesAsync();

        var archive = new ImdbLookupCacheArchive(1,
        [
            new ImdbLookupCacheRow("existing|2000", "Refreshed Title", 2000, MediaKind.Movie, "tt9999999",
                ImdbLookupStatus.Found, DateTimeOffset.UtcNow),
            new ImdbLookupCacheRow("new|2002", "Brand New", 2002, MediaKind.Series, "tt1234567",
                ImdbLookupStatus.Found, DateTimeOffset.UtcNow),
        ]);
        var package = await ScanArchivePackage.WriteAsync(archive, default);
        controller.ControllerContext.HttpContext.Request.Body = package;

        var result = Assert.IsType<OkObjectResult>((await controller.Import(default)).Result);
        var summary = Assert.IsType<ImdbLookupCacheImportResult>(result.Value);
        Assert.Equal(1, summary.Added);
        Assert.Equal(1, summary.Updated);

        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.ImdbLookupCache.CountAsync());
        var refreshed = await db.ImdbLookupCache.SingleAsync(c => c.CacheKey == "existing|2000");
        Assert.Equal("Refreshed Title", refreshed.ParsedTitle);
        Assert.Equal("tt9999999", refreshed.ImdbId);
        Assert.NotNull(await db.ImdbLookupCache.SingleOrDefaultAsync(c => c.CacheKey == "untouched|2001"));
        Assert.NotNull(await db.ImdbLookupCache.SingleOrDefaultAsync(c => c.CacheKey == "new|2002"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":99,\"entries\":[]}")]
    [InlineData("{\"version\":1,\"entries\":[{\"cacheKey\":\"\",\"parsedTitle\":\"x\",\"kind\":0,\"status\":0}]}")]
    [InlineData("{\"version\":1,\"entries\":[{\"cacheKey\":\"dup\",\"parsedTitle\":\"a\",\"kind\":0,\"status\":0},{\"cacheKey\":\"dup\",\"parsedTitle\":\"b\",\"kind\":0,\"status\":0}]}")]
    public async Task InvalidImportDoesNotPersistAnything(string json)
    {
        controller.ControllerContext.HttpContext.Request.ContentType = "application/json";
        controller.ControllerContext.HttpContext.Request.Body =
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.IsType<BadRequestObjectResult>((await controller.Import(default)).Result);
        Assert.Empty(db.ImdbLookupCache);
    }

    public void Dispose()
    {
        connection.Dispose();
        db.Dispose();
    }
}
