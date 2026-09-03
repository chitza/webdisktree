using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Security;

/// <summary>Operator-configured allow-list of host paths that scans (and, if AllowDelete, deletes) may target. Bound from configuration ("AllowedRoots" section), never user-editable via the API.</summary>
public class AllowedRootsOptions
{
    public List<AllowedRoot> Roots { get; set; } = new();
}
