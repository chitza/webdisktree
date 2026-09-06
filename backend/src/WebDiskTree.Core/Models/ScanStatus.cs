namespace WebDiskTree.Core.Models;

public enum ScanStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public enum ScanTrigger
{
    Manual,
    Scheduled,
    Imported,
}
