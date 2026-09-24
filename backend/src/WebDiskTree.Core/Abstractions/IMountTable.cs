namespace WebDiskTree.Core.Abstractions;

public record MountInfo(string MountPoint, string FileSystemType, bool IsReadOnly);

public interface IMountTable
{
    IReadOnlyList<MountInfo> GetMounts();

    /// <summary>The innermost mount whose mount point is <paramref name="path"/> or one of its ancestors.</summary>
    MountInfo? FindContaining(string path);
}
