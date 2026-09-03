using Microsoft.AspNetCore.Mvc;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api/roots")]
public class RootsController(AllowedRootsService allowedRoots) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<AllowedRootDto>> GetRoots()
    {
        var dtos = allowedRoots.GetRoots().Select(r => new AllowedRootDto(r.Path, r.Label, r.AllowDelete)).ToList();
        return Ok(dtos);
    }
}
