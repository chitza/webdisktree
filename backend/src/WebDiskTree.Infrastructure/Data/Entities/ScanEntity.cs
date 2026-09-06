using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Data.Entities;

public class ScanEntity
{
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
