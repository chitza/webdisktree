namespace WebDiskTree.Core.Models;

/// <summary>A server-operator-configured root path that scans (and, if AllowDelete, deletes) may target. Bound from configuration, not user-editable via the API.</summary>
public class AllowedRoot
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public bool AllowDelete { get; init; }
}
