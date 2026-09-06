namespace WebDiskTree.Core.Models;

public class DirectoryNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; set; }
    public DateTimeOffset ModifiedUtc { get; init; }
    public bool IsSymlink { get; init; }
    public List<DirectoryNode> Directories { get; init; } = new();

    /// <summary>Top files by size in this directory (capped — see <see cref="MaxFileChildren"/>); the remainder is folded into <see cref="OtherFilesCount"/>.</summary>
    public List<FileEntry> Files { get; init; } = new();
    public int OtherFilesCount { get; set; }
    public long OtherFilesSizeBytes { get; set; }

    public const int MaxFileChildren = 20;
}
