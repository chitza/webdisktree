namespace WebDiskTree.Infrastructure.Security;

/// <summary>The directory that bounds every scan, favorite and delete. In Docker this is where the host filesystem is mounted.</summary>
public class HostRootOptions
{
    public string Path { get; set; } = "/hostfs";
}
