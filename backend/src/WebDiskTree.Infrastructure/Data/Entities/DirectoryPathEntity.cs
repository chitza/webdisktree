namespace WebDiskTree.Infrastructure.Data.Entities;

/// <summary>
/// One row per unique (ScanId, Path) directory referenced by <see cref="FileEntryEntity.ParentDirectoryId"/>.
/// Interns the parent path string once per directory instead of repeating it on every file row in that
/// directory, which is what made FileEntries and its parent-path index disproportionately large.
/// </summary>
public class DirectoryPathEntity
{
    public long Id { get; set; }

    /// <summary>References <see cref="ScanEntity.SeqId"/>, not <see cref="ScanEntity.Id"/> — see that
    /// property's doc comment for why.</summary>
    public required int ScanSeq { get; set; }
    public required string Path { get; set; }
}
