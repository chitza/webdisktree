using WebDiskTree.Core.Models;

namespace WebDiskTree.Core.Abstractions;

public interface IScanProgressReporter
{
    void ReportProgress(Guid scanId, long filesScanned, long dirsScanned, long bytesScanned, string currentPath);
    void ReportCompleted(Guid scanId);
    void ReportFailed(Guid scanId, string errorMessage);
    void ReportCancelled(Guid scanId);
}

public interface IScanEngine
{
    /// <summary>
    /// Recursively walks <paramref name="rootPath"/>, reporting progress via <paramref name="progress"/>.
    /// Returns the root <see cref="DirectoryNode"/> of the scanned tree plus the flat file/dir list for
    /// bulk-inserting into the relational store; aggregate counters are written back onto <paramref name="job"/>.
    /// </summary>
    Task<ScanResult> ScanAsync(ScanJob job, IScanProgressReporter progress, CancellationToken cancellationToken);
}

public record ScanResult(DirectoryNode Root, IReadOnlyList<FlatFileRow> FlatRows);

/// <summary>A single file or directory row, flattened for bulk relational insert (list view / type breakdown).</summary>
public record FlatFileRow(
    string ParentPath,
    string Name,
    string? Extension,
    long SizeBytes,
    DateTimeOffset ModifiedUtc,
    bool IsDirectory);
