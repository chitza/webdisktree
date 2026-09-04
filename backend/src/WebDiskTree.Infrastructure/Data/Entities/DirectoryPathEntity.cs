namespace WebDiskTree.Infrastructure.Data.Entities;

/// <summary>
/// One row per unique (ScanId, Path) directory referenced by <see cref="FileEntryEntity.ParentDirectoryId"/>.
/// Interns the parent path string once per directory instead of repeating it on every file row in that
/// directory, which is what made FileEntries and its parent-path index disproportionately large.
/// </summary>
public class DirectoryPathEntity
{
    public long Id { get; set; }
    public required Guid ScanId { get; set; }
    public required string Path { get; set; }
}
