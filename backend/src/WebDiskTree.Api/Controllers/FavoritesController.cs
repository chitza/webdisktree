using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController(WebDiskTreeDbContext dbContext, HostRootService hostRoot) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FavoriteDto>>> GetFavorites(CancellationToken cancellationToken)
    {
        var favorites = await dbContext.FavoritePaths.OrderBy(f => f.Label).ToListAsync(cancellationToken);
        return Ok(favorites.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteDto>> CreateFavorite(CreateFavoriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest("Path is required.");
        }

        if (!hostRoot.IsUnderHostRoot(request.Path))
        {
            return BadRequest("Path is not under the host root.");
        }

        var path = PathUtil.Normalize(request.Path);
        if (!Directory.Exists(path))
        {
            return BadRequest("Path does not exist or is not a directory.");
        }

        if (await dbContext.FavoritePaths.AnyAsync(f => f.Path == path, cancellationToken))
        {
            return Conflict("Path is already a favorite.");
        }

        var entity = new FavoritePathEntity
        {
            Id = Guid.NewGuid(),
            Path = path,
            Label = string.IsNullOrWhiteSpace(request.Label) ? hostRoot.ToHostPath(path) : request.Label.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.FavoritePaths.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetFavorites), new { }, ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FavoriteDto>> UpdateFavorite(Guid id, UpdateFavoriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return BadRequest("Label is required.");
        }

        var entity = await dbContext.FavoritePaths.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Label = request.Label.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFavorite(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.FavoritePaths.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        dbContext.FavoritePaths.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static FavoriteDto ToDto(FavoritePathEntity e) => new(e.Id, e.Path, e.Label, e.CreatedAt);
}
