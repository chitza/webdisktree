namespace WebDiskTree.Infrastructure.Data.Entities;

public class ScheduleEntity
{
    public Guid Id { get; set; }
    public required string RootPath { get; set; }
    public required string CronExpression { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
