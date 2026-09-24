using Microsoft.Extensions.Options;

namespace WebDiskTree.Infrastructure.Security;

public class HostRootService(IOptions<HostRootOptions> options)
{
    public string Root { get; } = PathUtil.Normalize(options.Value.Path);

    public bool IsUnderHostRoot(string path) => PathUtil.IsSameOrUnder(Root, PathUtil.Normalize(path));

    /// <summary>The path as the host sees it: <paramref name="path"/> with the host root prefix removed, "/" for the root itself.</summary>
    public string ToHostPath(string path)
    {
        var normalized = PathUtil.Normalize(path);
        if (Path.EndsInDirectorySeparator(Root) || !PathUtil.IsSameOrUnder(Root, normalized))
        {
            return normalized;
        }

        var rest = normalized[Root.Length..];
        return rest.Length == 0 ? Path.DirectorySeparatorChar.ToString() : rest;
    }
}
