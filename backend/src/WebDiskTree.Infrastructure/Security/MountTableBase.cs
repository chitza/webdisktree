using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

public abstract class MountTableBase : IMountTable
{
    public abstract IReadOnlyList<MountInfo> GetMounts();

    public MountInfo? FindContaining(string path)
    {
        var normalized = PathUtil.Normalize(path);
        return GetMounts()
            .Where(m => PathUtil.IsSameOrUnder(PathUtil.Normalize(m.MountPoint), normalized))
            .MaxBy(m => PathUtil.Normalize(m.MountPoint).Length);
    }
}
