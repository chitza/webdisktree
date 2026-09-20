namespace WebDiskTree.Infrastructure.Scheduling;

/// <summary>Operator-configured cap on how many finished scans a single schedule keeps. Bound from configuration
/// ("ScheduleRetention" section).</summary>
public class ScheduleRetentionOptions
{
    public int MaxScansPerSchedule { get; set; } = 3;
}
