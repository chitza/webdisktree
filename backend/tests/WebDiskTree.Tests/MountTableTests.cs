using WebDiskTree.Core.Abstractions;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Tests;

public class MountTableTests
{
    // Real /proc/self/mountinfo from a container started with -v /:/hostfs:ro and a :rw mount of ~/Downloads, trimmed.
    private static readonly string Fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mountinfo.txt"));

    [Fact]
    public void ParsesEveryLineOfTheFixture()
    {
        var mounts = LinuxMountTable.Parse(Fixture);

        Assert.Equal(Fixture.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length, mounts.Count);
    }

    [Fact]
    public void ReadsMountPointFileSystemTypeAndReadOnlyFlag()
    {
        var mounts = LinuxMountTable.Parse(Fixture);

        Assert.Contains(new MountInfo("/hostfs", "ext4", IsReadOnly: true), mounts);
        Assert.Contains(new MountInfo("/hostfs/home/cristi/Downloads", "ext4", IsReadOnly: false), mounts);
        Assert.Contains(new MountInfo("/hostfs/boot/efi", "vfat", IsReadOnly: true), mounts);
        Assert.Contains(new MountInfo("/hostfs/proc/fs/nfsd", "nfsd", IsReadOnly: true), mounts);
        Assert.Contains(new MountInfo("/", "overlay", IsReadOnly: false), mounts);
    }

    [Fact]
    public void UnescapesOctalSequencesInMountPoints()
    {
        var mounts = LinuxMountTable.Parse(Fixture);
        Assert.Contains(new MountInfo("/hostfs/media/cristi/WD Black", "ext4", IsReadOnly: true), mounts);

        var line = @"36 35 98:0 / /mnt/a\040b\011c\134d rw,noatime master:1 shared:2 - ext3 /dev/root rw,errors=continue";
        Assert.Equal([new MountInfo("/mnt/a b\tc\\d", "ext3", IsReadOnly: false)], LinuxMountTable.Parse(line));
    }

    [Fact]
    public void FindContainingReturnsTheInnermostMount()
    {
        var table = new FixtureMountTable();

        Assert.Equal("/hostfs/media/cristi/WD Black", table.FindContaining("/hostfs/media/cristi/WD Black/Movies/x.mkv")?.MountPoint);
        Assert.Equal("/hostfs/home/cristi/Downloads", table.FindContaining("/hostfs/home/cristi/Downloads")?.MountPoint);
        Assert.Equal("/hostfs", table.FindContaining("/hostfs/home/cristi")?.MountPoint);
        Assert.Equal("/", table.FindContaining("/etc")?.MountPoint);
    }

    [Fact]
    public void FindContainingMatchesWholePathSegmentsOnly()
    {
        var table = new ListMountTable([new MountInfo("/", "ext4", true), new MountInfo("/a/b", "ext4", false)]);

        Assert.Equal("/", table.FindContaining("/a/bc")?.MountPoint);
        Assert.Equal("/a/b", table.FindContaining("/a/b/c")?.MountPoint);
    }

    private sealed class FixtureMountTable : MountTableBase
    {
        public override IReadOnlyList<MountInfo> GetMounts() => LinuxMountTable.Parse(Fixture);
    }

    private sealed class ListMountTable(IReadOnlyList<MountInfo> mounts) : MountTableBase
    {
        public override IReadOnlyList<MountInfo> GetMounts() => mounts;
    }
}
