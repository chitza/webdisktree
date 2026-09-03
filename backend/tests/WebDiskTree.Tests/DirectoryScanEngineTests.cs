using WebDiskTree.Core.Abstractions;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Scanning;

namespace WebDiskTree.Tests;

public class DirectoryScanEngineTests : IDisposable
{
    private readonly string _root;

    public DirectoryScanEngineTests()
    {
        _root = Directory.CreateTempSubdirectory("webdisktree-scan-test-").FullName;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class NoopProgressReporter : IScanProgressReporter
    {
        public void ReportProgress(Guid scanId, long filesScanned, long dirsScanned, long bytesScanned, string currentPath) { }
        public void ReportCompleted(Guid scanId) { }
        public void ReportFailed(Guid scanId, string errorMessage) { }
        public void ReportCancelled(Guid scanId) { }
    }

    [Fact]
    public async Task ScanAsync_AggregatesSizesBottomUp()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllBytes(Path.Combine(_root, "top.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_root, "sub", "nested.bin"), new byte[250]);

        var engine = new DirectoryScanEngine();
        var job = new ScanJob { RootPath = _root };

        var result = await engine.ScanAsync(job, new NoopProgressReporter(), CancellationToken.None);

        Assert.Equal(350, result.Root.SizeBytes);
        Assert.Equal(350, job.TotalBytes);
        Assert.Equal(2, job.TotalFiles);
        Assert.Equal(2, job.TotalDirs); // root + sub
        var subNode = Assert.Single(result.Root.Directories);
        Assert.Equal(250, subNode.SizeBytes);
    }

    [Fact]
    public async Task ScanAsync_CapsFileChildrenAndAggregatesTheRest()
    {
        for (var i = 0; i < DirectoryNode.MaxFileChildren + 10; i++)
        {
            File.WriteAllBytes(Path.Combine(_root, $"file{i}.bin"), new byte[10]);
        }

        var engine = new DirectoryScanEngine();
        var job = new ScanJob { RootPath = _root };

        var result = await engine.ScanAsync(job, new NoopProgressReporter(), CancellationToken.None);

        Assert.Equal(DirectoryNode.MaxFileChildren, result.Root.Files.Count);
        Assert.Equal(10, result.Root.OtherFilesCount);
        Assert.Equal(100, result.Root.OtherFilesSizeBytes);
        Assert.Equal(300, result.Root.SizeBytes); // 30 files * 10 bytes
    }

    [Fact]
    public async Task ScanAsync_ContinuesPastUnreadableSubdirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // chmod has no equivalent on Windows; this scenario is exercised on Linux/macOS CI instead.
        }

        var restricted = Path.Combine(_root, "restricted");
        Directory.CreateDirectory(restricted);
        File.WriteAllBytes(Path.Combine(restricted, "secret.bin"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_root, "visible.bin"), new byte[20]);

        try
        {
#pragma warning disable CA1416 // guarded by OperatingSystem.IsWindows() check above
            File.SetUnixFileMode(restricted, UnixFileMode.None);
#pragma warning restore CA1416

            var engine = new DirectoryScanEngine();
            var job = new ScanJob { RootPath = _root };

            var result = await engine.ScanAsync(job, new NoopProgressReporter(), CancellationToken.None);

            // The scan must not throw, and the readable sibling file must still be counted.
            Assert.Contains(result.FlatRows, r => r.Name == "visible.bin");
        }
        finally
        {
#pragma warning disable CA1416
            File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
        }
    }
}
