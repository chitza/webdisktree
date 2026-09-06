using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Data.Entities;

public class ScanEntity
{
    /// <summary>Autoincrement surrogate stored as the FK in FileEntries/DirectoryPaths instead of <see cref="Id"/>
    /// — with millions of file rows per scan, indexing a 36-byte Guid there instead of a small int cost hundreds
    /// of MB. <see cref="Id"/> remains the public-facing identifier used in URLs and DTOs.</summary>
    public int SeqId { get; set; }
    public Guid Id { get; set; }
    public required string RootPath { get; set; }
    public ScanTrigger Trigger { get; set; }
    public ScanStatus Status { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long TotalBytes { get; set; }
    public long TotalFiles { get; set; }
    public long TotalDirs { get; set; }
    public int ErrorCount { get; set; }
    public string? BlobPath { get; set; }
    public bool IsPinned { get; set; }
    public bool IsStale { get; set; }
    public string? ErrorMessage { get; set; }
}
