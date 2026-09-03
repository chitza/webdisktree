namespace WebDiskTree.Core.Models;

public class ScanSchedule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RootPath { get; init; }
    public required string CronExpression { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
