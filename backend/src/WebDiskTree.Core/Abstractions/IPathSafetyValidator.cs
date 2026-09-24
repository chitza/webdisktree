namespace WebDiskTree.Core.Abstractions;

public interface IPathSafetyValidator
{
    /// <summary>
    /// Validates that <paramref name="candidatePath"/> may be deleted in the context of the scan rooted at
    /// <paramref name="scanRootPath"/>: canonicalizes the path, rejects traversal/symlink escapes, and requires
    /// the canonical path to be a strict descendant of the scan root, under the host root, and on a read-write mount.
    /// Returns the canonical path on success.
    /// </summary>
    bool TryValidateForDelete(string scanRootPath, string candidatePath, out string canonicalPath, out string? error);
}
