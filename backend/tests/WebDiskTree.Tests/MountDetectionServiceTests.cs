using Microsoft.Extensions.Options;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Tests;

public class MountDetectionServiceTests
{
    private static readonly string Fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mountinfo.txt"));

    private static MountDetectionService CreateService(IReadOnlyList<MountInfo> mounts, string hostRoot = "/hostfs") =>
        new(new ListMountTable(mounts), new HostRootService(Options.Create(new HostRootOptions { Path = hostRoot })));

    [Fact]
    public void ListsDiskMountsUnderTheHostRootWithHostPathLabels()
    {
        var roots = CreateService(LinuxMountTable.Parse(Fixture)).GetScanRoots();

        Assert.Equal(
        [
            new DetectedMount("/hostfs", "/", IsReadOnly: true),
            new DetectedMount("/hostfs/data", "/data", IsReadOnly: true),
            new DetectedMount("/hostfs/data_media/media", "/data_media/media", IsReadOnly: true),
            new DetectedMount("/hostfs/home/cristi/Downloads", "/home/cristi/Downloads", IsReadOnly: false),
            new DetectedMount("/hostfs/media/cristi/WD Black", "/media/cristi/WD Black", IsReadOnly: true),
            new DetectedMount("/hostfs/media/cristi/_lacie", "/media/cristi/_lacie", IsReadOnly: true),
        ], roots);
    }

    [Fact]
    public void DropsPseudoFileSystemsSystemPathsAndMountsOutsideTheHostRoot()
    {
        var roots = CreateService(
        [
            new MountInfo("/", "overlay", false),
            new MountInfo("/etc/hosts", "ext4", false),
            new MountInfo("/data", "ext4", false),
            new MountInfo("/hostfs", "ext4", true),
            new MountInfo("/hostfs/proc", "proc", true),
            new MountInfo("/hostfs/proc/fs/nfsd", "nfsd", true),
            new MountInfo("/hostfs/dev/shm", "tmpfs", true),
            new MountInfo("/hostfs/boot/efi", "vfat", true),
            new MountInfo("/hostfs/snap/core24/2124", "squashfs", true),
            new MountInfo("/hostfs/snap/other", "ext4", true),
            new MountInfo("/hostfs/var/lib/docker/overlay2/abc/merged", "overlay", true),
            new MountInfo("/hostfs/var/lib/docker/volumes/x", "ext4", true),
            new MountInfo("/hostfs/run/user/1000/gvfs", "fuse.gvfsd-fuse", true),
            new MountInfo("/hostfs/runner", "xfs", true),
            new MountInfo("/hostfs/mnt/nas", "nfs4", true),
            new MountInfo("/hostfsx", "ext4", true),
        ]).GetScanRoots();

        Assert.Equal(["/hostfs", "/hostfs/mnt/nas", "/hostfs/runner"], roots.Select(r => r.Path));
    }

    [Fact]
    public void LaterMountOnTheSameMountPointWins()
    {
        var roots = CreateService(
        [
            new MountInfo("/hostfs", "ext4", true),
            new MountInfo("/hostfs/media/usb", "ext4", true),
            new MountInfo("/hostfs/media/usb", "exfat", false),
        ]).GetScanRoots();

        Assert.Equal(new DetectedMount("/hostfs/media/usb", "/media/usb", IsReadOnly: false), roots[1]);
        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public void HostRootOfSlashKeepsFullPathsAsLabels()
    {
        var roots = CreateService(
        [
            new MountInfo("/", "ext4", false),
            new MountInfo("/media/cristi/WD Black", "ext4", false),
            new MountInfo("/boot/efi", "vfat", false),
        ], hostRoot: "/").GetScanRoots();

        Assert.Equal(
        [
            new DetectedMount("/", "/", IsReadOnly: false),
            new DetectedMount("/media/cristi/WD Black", "/media/cristi/WD Black", IsReadOnly: false),
        ], roots);
    }

    private sealed class ListMountTable(IReadOnlyList<MountInfo> mounts) : MountTableBase
    {
        public override IReadOnlyList<MountInfo> GetMounts() => mounts;
    }
}
