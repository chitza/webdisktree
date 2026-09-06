namespace WebDiskTree.Infrastructure.Data.Entities;

/// <summary>
/// Maps the FileEntries table for EF-based querying (list view / type breakdown). Rows are bulk-inserted via raw
/// ADO.NET in <see cref="FileEntryBulkWriter"/> for scan-time throughput, not via EF change tracking.
/// </summary>
public class FileEntryEntity
{
    public long Id { get; set; }

    /// <summary>References <see cref="ScanEntity.SeqId"/>, not <see cref="ScanEntity.Id"/> — see that
    /// property's doc comment for why.</summary>
    public required int ScanSeq { get; set; }

    /// <summary>References <see cref="DirectoryPathEntity.Id"/> instead of repeating the parent path string on
    /// every file row — a directory with N files previously stored (and indexed) that path text N times.</summary>
    public required long ParentDirectoryId { get; set; }

    public required string Name { get; set; }
    public string? Extension { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset ModifiedUtc { get; set; }
    public bool IsDirectory { get; set; }
}
