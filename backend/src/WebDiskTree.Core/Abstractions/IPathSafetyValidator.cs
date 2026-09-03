namespace WebDiskTree.Core.Abstractions;

public interface IPathSafetyValidator
{
    /// <summary>
    /// Validates that <paramref name="candidatePath"/> may be deleted in the context of the scan rooted at
    /// <paramref name="scanRootPath"/>: canonicalizes the path, rejects traversal/symlink escapes, and requires
    /// the canonical path to be a strict descendant of both the scan root and a configured allow-listed root
    /// that permits delete. Returns the canonical path on success.
    /// </summary>
    bool TryValidateForDelete(string scanRootPath, string candidatePath, out string canonicalPath, out string? error);
}
