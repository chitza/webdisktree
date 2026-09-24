namespace WebDiskTree.Infrastructure.Security;

public static class PathUtil
{
    public static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>True if <paramref name="path"/> equals <paramref name="root"/> or is below it. Both must be normalized.</summary>
    public static bool IsSameOrUnder(string root, string path) =>
        string.Equals(root, path, Comparison) || IsStrictlyUnder(root, path);

    /// <summary>True if <paramref name="path"/> is below <paramref name="root"/>, on whole path segments. Both must be normalized.</summary>
    public static bool IsStrictlyUnder(string root, string path)
    {
        // A filesystem root ("/") keeps its trailing separator after normalization.
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.Length > prefix.Length && path.StartsWith(prefix, Comparison);
    }
}
