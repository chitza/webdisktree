using Microsoft.Extensions.Options;
using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

public class PathSafetyValidator(IOptions<AllowedRootsOptions> allowedRoots) : IPathSafetyValidator
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public bool TryValidateForDelete(string scanRootPath, string candidatePath, out string canonicalPath, out string? error)
    {
        canonicalPath = string.Empty;

        if (string.IsNullOrWhiteSpace(candidatePath) ||
            candidatePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            error = "Path contains traversal segments.";
            return false;
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scanRootPath));
        var resolved = Path.GetFullPath(candidatePath);

        if (Directory.Exists(resolved))
        {
            var linkTarget = new DirectoryInfo(resolved).ResolveLinkTarget(returnFinalTarget: true);
            if (linkTarget is not null)
            {
                resolved = linkTarget.FullName;
            }
        }
        else if (File.Exists(resolved))
        {
            var linkTarget = new FileInfo(resolved).ResolveLinkTarget(returnFinalTarget: true);
            if (linkTarget is not null)
            {
                resolved = linkTarget.FullName;
            }
        }
        else
        {
            error = "Path does not exist.";
            return false;
        }

        resolved = Path.TrimEndingDirectorySeparator(resolved);

        if (string.Equals(resolved, normalizedRoot, PathComparison))
        {
            error = "Refusing to delete the scan root itself.";
            return false;
        }

        if (!IsStrictDescendant(normalizedRoot, resolved))
        {
            error = "Path is outside the scanned root.";
            return false;
        }

        var allowedRoot = allowedRoots.Value.Roots.FirstOrDefault(r =>
        {
            var normalizedAllowed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path));
            return string.Equals(normalizedAllowed, normalizedRoot, PathComparison) || IsStrictDescendant(normalizedAllowed, normalizedRoot);
        });

        if (allowedRoot is null || !allowedRoot.AllowDelete)
        {
            error = "Path is not under an allowed, delete-enabled root.";
            return false;
        }

        canonicalPath = resolved;
        error = null;
        return true;
    }

    private static bool IsStrictDescendant(string root, string path)
    {
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, PathComparison);
    }
}
