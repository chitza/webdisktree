using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

/// <summary>For local development off Linux (macOS). No read-only flag is available, so every drive counts as read-write.</summary>
public class FallbackMountTable : MountTableBase
{
    public override IReadOnlyList<MountInfo> GetMounts() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network)
            .Select(d => new MountInfo(d.RootDirectory.FullName, d.DriveFormat, IsReadOnly: false))
            .ToList();
}
