using System.Diagnostics;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Scanning;

/// <summary>
/// Iterative (explicit-stack, not recursive-call) directory walker. Symlinks/reparse points are not followed —
/// recorded as zero-size leaf entries — avoiding cross-platform cycle detection. Permission errors are caught
/// per-directory and accumulated rather than aborting the whole scan.
/// </summary>
public class DirectoryScanEngine : IScanEngine
{
    private const int ProgressEveryEntries = 2000;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private const int MaxSampleErrors = 500;

    // AttributesToSkip left at None (overriding the default Hidden|System skip) so hidden/system files count
    // toward disk usage; reparse points are deliberately NOT skipped here either — they must be enumerated so
    // the loop below can record them as zero-size leaves without recursing into them (see IsSymlink handling).
    private static readonly EnumerationOptions EnumOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.None,
        RecurseSubdirectories = false,
    };

    public Task<ScanResult> ScanAsync(ScanJob job, IScanProgressReporter progress, CancellationToken cancellationToken)
    {
        var root = new DirectoryNode
        {
            Name = Path.GetFileName(job.RootPath.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name
                ? name
                : job.RootPath,
            FullPath = job.RootPath,
            ModifiedUtc = SafeGetLastWriteTime(job.RootPath),
        };

        var flatRows = new List<FlatFileRow>();
        var stack = new Stack<DirectoryNode>();
        stack.Push(root);

        long filesScanned = 0;
        long dirsScanned = 0;
        long bytesScanned = 0;
        long lastReportedEntries = 0;
        var stopwatch = Stopwatch.StartNew();

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            dirsScanned++;

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(current.FullPath).EnumerateFileSystemInfos("*", EnumOptions);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                RecordError(job, current.FullPath, ex);
                continue;
            }

            var candidateChildren = new List<(FileSystemInfo Info, bool IsDir)>();
            try
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var isDir = (entry.Attributes & FileAttributes.Directory) != 0;
                    candidateChildren.Add((entry, isDir));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                RecordError(job, current.FullPath, ex);
            }

            foreach (var (info, isDir) in candidateChildren)
            {
                var isSymlink = info.LinkTarget is not null;

                if (isDir)
                {
                    var childNode = new DirectoryNode
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        ModifiedUtc = SafeGetLastWriteTime(info),
                        IsSymlink = isSymlink,
                    };
                    current.Directories.Add(childNode);
                    flatRows.Add(new FlatFileRow(current.FullPath, info.Name, null, 0, childNode.ModifiedUtc, IsDirectory: true));

                    if (!isSymlink)
                    {
                        stack.Push(childNode);
                    }
                }
                else
                {
                    long size = isSymlink ? 0 : SafeGetLength(info);
                    filesScanned++;
                    bytesScanned += size;

                    flatRows.Add(new FlatFileRow(
                        current.FullPath, info.Name, Path.GetExtension(info.Name), size, SafeGetLastWriteTime(info), IsDirectory: false));

                    AddFileToNode(current, new FileEntry
                    {
                        Name = info.Name,
                        Extension = Path.GetExtension(info.Name),
                        SizeBytes = size,
                        ModifiedUtc = SafeGetLastWriteTime(info),
                        IsDirectory = false,
                        IsSymlink = isSymlink,
                    });
                }
            }

            var totalEntries = filesScanned + dirsScanned;
            if (totalEntries - lastReportedEntries >= ProgressEveryEntries || stopwatch.Elapsed >= ProgressInterval)
            {
                progress.ReportProgress(job.Id, filesScanned, dirsScanned, bytesScanned, current.FullPath);
                lastReportedEntries = totalEntries;
                stopwatch.Restart();
            }
        }

        RollUpDirectorySizes(root);

        job.TotalBytes = root.SizeBytes;
        job.TotalFiles = filesScanned;
        job.TotalDirs = dirsScanned;

        return Task.FromResult(new ScanResult(root, flatRows));
    }

    private static void AddFileToNode(DirectoryNode node, FileEntry file)
    {
        if (node.Files.Count < DirectoryNode.MaxFileChildren)
        {
            node.Files.Add(file);
            return;
        }

        // Once capped, keep the top-N by size: evict the smallest kept file if this one is bigger.
        var smallestIndex = 0;
        for (var i = 1; i < node.Files.Count; i++)
        {
            if (node.Files[i].SizeBytes < node.Files[smallestIndex].SizeBytes)
            {
                smallestIndex = i;
            }
        }

        if (file.SizeBytes > node.Files[smallestIndex].SizeBytes)
        {
            var evicted = node.Files[smallestIndex];
            node.Files[smallestIndex] = file;
            node.OtherFilesCount++;
            node.OtherFilesSizeBytes += evicted.SizeBytes;
        }
        else
        {
            node.OtherFilesCount++;
            node.OtherFilesSizeBytes += file.SizeBytes;
        }
    }

    /// <summary>Directory sizes are only known once all their file children (added incrementally as the parent
    /// is visited) are in — this walks the finished tree bottom-up once to sum directory sizes from children.</summary>
    private static long RollUpDirectorySizes(DirectoryNode node)
    {
        long size = node.OtherFilesSizeBytes;
        foreach (var file in node.Files)
        {
            size += file.SizeBytes;
        }

        foreach (var dir in node.Directories)
        {
            size += RollUpDirectorySizes(dir);
        }

        node.SizeBytes = size;
        return size;
    }

    private void RecordError(ScanJob job, string path, Exception ex)
    {
        job.ErrorCount++;
        if (job.SampleErrors.Count < MaxSampleErrors)
        {
            job.SampleErrors.Add($"{path}: {ex.Message}");
        }
    }

    private static DateTimeOffset SafeGetLastWriteTime(string path)
    {
        try
        {
            return new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    private static DateTimeOffset SafeGetLastWriteTime(FileSystemInfo info)
    {
        try
        {
            return new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
        }
        catch
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    private static long SafeGetLength(FileSystemInfo info)
    {
        try
        {
            return info is FileInfo fi ? fi.Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
