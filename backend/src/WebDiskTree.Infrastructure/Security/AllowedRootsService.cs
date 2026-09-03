using Microsoft.Extensions.Options;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Security;

public class AllowedRootsService(IOptions<AllowedRootsOptions> options)
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public IReadOnlyList<AllowedRoot> GetRoots() => options.Value.Roots;

    /// <summary>True if <paramref name="path"/> is exactly, or a descendant of, one of the configured allowed roots.</summary>
    public bool IsAllowed(string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return options.Value.Roots.Any(r =>
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path));
            return string.Equals(normalizedRoot, normalizedPath, PathComparison) ||
                   normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, PathComparison);
        });
    }
}
