using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

public record DetectedMount(string Path, string Label, bool IsReadOnly);

public class MountDetectionService(IMountTable mountTable, HostRootService hostRoot)
{
    private static readonly HashSet<string> DiskFileSystems =
    [
        "ext2", "ext3", "ext4", "xfs", "btrfs", "zfs", "ntfs", "ntfs3", "fuseblk", "exfat", "vfat", "hfsplus", "apfs",
        "nfs", "nfs4", "cifs", "smb3",
    ];

    private static readonly string[] ExcludedUnderHostRoot = ["boot", "snap", "var/lib/docker", "run", "proc", "sys"];

    public IReadOnlyList<DetectedMount> GetScanRoots()
    {
        var excluded = ExcludedUnderHostRoot.Select(p => PathUtil.Normalize(Path.Combine(hostRoot.Root, p))).ToList();

        return mountTable.GetMounts()
            // DriveInfo on macOS reports formats that aren't Linux fstype names, so the type filter is Linux-only.
            .Where(m => !OperatingSystem.IsLinux() || DiskFileSystems.Contains(m.FileSystemType))
            .Where(m => hostRoot.IsUnderHostRoot(m.MountPoint))
            .Where(m => !excluded.Any(e => PathUtil.IsSameOrUnder(e, PathUtil.Normalize(m.MountPoint))))
            // A later mount on the same mount point hides the earlier one.
            .GroupBy(m => PathUtil.Normalize(m.MountPoint))
            .Select(g => g.Last())
            .Select(m => new DetectedMount(PathUtil.Normalize(m.MountPoint), hostRoot.ToHostPath(m.MountPoint), m.IsReadOnly))
            .OrderBy(m => m.Path, StringComparer.Ordinal)
            .ToList();
    }
}
