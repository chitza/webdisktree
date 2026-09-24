using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebDiskTree.Api.Controllers;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Tests;

public class FavoritePathTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebDiskTreeDbContext db;
    private readonly FavoritesController controller;
    private readonly string hostRoot;

    public FavoritePathTests()
    {
        hostRoot = Directory.CreateTempSubdirectory("webdisktree-favorites-test-").FullName;
        Directory.CreateDirectory(Path.Combine(hostRoot, "media", "WD Black"));

        connection.Open();
        db = new(new DbContextOptionsBuilder<WebDiskTreeDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();
        controller = new(db, new HostRootService(Options.Create(new HostRootOptions { Path = hostRoot })));
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
        Directory.Delete(hostRoot, recursive: true);
    }

    private static T Value<T>(ActionResult<T> result) where T : class =>
        Assert.IsType<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    [Fact]
    public async Task AddStoresTheFullPathAndDefaultsTheLabelToTheHostPath()
    {
        var result = await controller.CreateFavorite(new(Path.Combine(hostRoot, "media", "WD Black") + "/", null), default);

        var dto = Value(result);
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(Path.Combine(hostRoot, "media", "WD Black"), dto.Path);
        Assert.Equal("/media/WD Black", dto.Label);
        db.ChangeTracker.Clear();
        Assert.Equal(dto.Path, (await db.FavoritePaths.SingleAsync()).Path);
    }

    [Fact]
    public async Task AddKeepsAGivenLabel()
    {
        var dto = Value(await controller.CreateFavorite(new(Path.Combine(hostRoot, "media"), "  Media  "), default));
        Assert.Equal("Media", dto.Label);
    }

    [Fact]
    public async Task AddRejectsAPathOutsideTheHostRoot()
    {
        var result = await controller.CreateFavorite(new("/etc", null), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.FavoritePaths);
    }

    [Fact]
    public async Task AddRejectsAMissingDirectory()
    {
        var result = await controller.CreateFavorite(new(Path.Combine(hostRoot, "missing"), null), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.FavoritePaths);
    }

    [Fact]
    public async Task AddRejectsADuplicate()
    {
        var path = Path.Combine(hostRoot, "media");
        Value(await controller.CreateFavorite(new(path, null), default));

        var result = await controller.CreateFavorite(new(path + "/", "again"), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.FavoritePaths);
    }

    [Fact]
    public async Task RenameChangesOnlyTheLabel()
    {
        var created = Value(await controller.CreateFavorite(new(Path.Combine(hostRoot, "media"), "Old"), default));

        var renamed = Value(await controller.UpdateFavorite(created.Id, new("New"), default));

        Assert.Equal(created with { Label = "New" }, renamed);
        db.ChangeTracker.Clear();
        var stored = await db.FavoritePaths.SingleAsync();
        Assert.Equal(("New", created.Path, created.CreatedAt), (stored.Label, stored.Path, stored.CreatedAt));
        Assert.IsType<BadRequestObjectResult>((await controller.UpdateFavorite(created.Id, new(" "), default)).Result);
        Assert.IsType<NotFoundResult>((await controller.UpdateFavorite(Guid.NewGuid(), new("x"), default)).Result);
    }

    [Fact]
    public async Task DeleteRemovesTheFavorite()
    {
        var created = Value(await controller.CreateFavorite(new(Path.Combine(hostRoot, "media"), null), default));

        Assert.IsType<NoContentResult>(await controller.DeleteFavorite(created.Id, default));
        Assert.Empty(db.FavoritePaths);
        Assert.IsType<NotFoundResult>(await controller.DeleteFavorite(created.Id, default));
    }
}
