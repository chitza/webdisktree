namespace WebDiskTree.Core.Models;

public class FileEntry
{
    public required string Name { get; init; }
    public string? Extension { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset ModifiedUtc { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsSymlink { get; init; }
}
