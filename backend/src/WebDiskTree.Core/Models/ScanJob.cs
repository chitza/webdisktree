namespace WebDiskTree.Core.Models;

public class ScanJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RootPath { get; init; }
    public ScanTrigger Trigger { get; init; } = ScanTrigger.Manual;
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long TotalBytes { get; set; }
    public long TotalFiles { get; set; }
    public long TotalDirs { get; set; }
    public int ErrorCount { get; set; }
    public List<string> SampleErrors { get; } = new();
    public string? BlobPath { get; set; }
    public bool IsStale { get; set; }
    public string? ErrorMessage { get; set; }
}

public record ScanJobRequest(Guid ScanId, string RootPath, ScanTrigger Trigger);
