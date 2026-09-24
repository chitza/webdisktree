using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api/roots")]
public class RootsController(
    WebDiskTreeDbContext dbContext,
    MountDetectionService mountDetection,
    IMountTable mountTable) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScanRootDto>>> GetRoots(CancellationToken cancellationToken)
    {
        var mounts = mountDetection.GetScanRoots()
            .Select(m => new ScanRootDto(m.Path, m.Label, "mount", AllowDelete: !m.IsReadOnly));

        var favorites = (await dbContext.FavoritePaths.OrderBy(f => f.Label).ToListAsync(cancellationToken))
            .Select(f => new ScanRootDto(f.Path, f.Label, "favorite", AllowDelete: mountTable.FindContaining(f.Path) is { IsReadOnly: false }));

        return Ok(mounts.Concat(favorites).ToList());
    }
}
