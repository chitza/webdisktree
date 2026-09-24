using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

public class LinuxMountTable : MountTableBase
{
    public override IReadOnlyList<MountInfo> GetMounts() => Parse(File.ReadAllText("/proc/self/mountinfo"));

    public static IReadOnlyList<MountInfo> Parse(string text) => throw new NotImplementedException();
}
