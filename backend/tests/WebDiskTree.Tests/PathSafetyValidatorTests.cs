using Microsoft.Extensions.Options;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Tests;

public class PathSafetyValidatorTests : IDisposable
{
    private readonly string _root;
    private readonly string _parent;
    private readonly PathSafetyValidator _validator;

    public PathSafetyValidatorTests()
    {
        _root = Directory.CreateTempSubdirectory("webdisktree-safety-test-").FullName;
        _parent = Directory.GetParent(_root)!.FullName;
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "child.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "top.txt"), "x");

        _validator = CreateValidator(hostRoot: _root, new MountInfo(_root, "ext4", IsReadOnly: false));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static PathSafetyValidator CreateValidator(string hostRoot, params MountInfo[] mounts) =>
        new(new FakeMountTable(mounts), new HostRootService(Options.Create(new HostRootOptions { Path = hostRoot })));

    [Fact]
    public void RejectsPathOutsideScanRoot()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "not-under-root.txt");
        File.WriteAllText(outsidePath, "x");
        try
        {
            var ok = _validator.TryValidateForDelete(_root, outsidePath, out _, out var error);
            Assert.False(ok);
            Assert.Contains("outside", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public void RejectsTraversalSegments()
    {
        var traversal = Path.Combine(_root, "sub", "..", "..", "etc", "passwd");
        var ok = _validator.TryValidateForDelete(_root, traversal, out _, out var error);
        Assert.False(ok);
        Assert.Contains("traversal", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsDeletingTheRootItself()
    {
        var ok = _validator.TryValidateForDelete(_root, _root, out _, out var error);
        Assert.False(ok);
        Assert.Contains("root itself", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowsValidDescendantOnReadWriteMount()
    {
        var childPath = Path.Combine(_root, "sub", "child.txt");
        var ok = _validator.TryValidateForDelete(_root, childPath, out var canonical, out var error);
        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(childPath), canonical);
    }

    [Fact]
    public void RejectsPathOutsideHostRoot()
    {
        var validator = CreateValidator(hostRoot: Path.Combine(_root, "sub"), new MountInfo(_parent, "ext4", IsReadOnly: false));

        var ok = validator.TryValidateForDelete(_root, Path.Combine(_root, "top.txt"), out _, out var error);

        Assert.False(ok);
        Assert.Contains("host root", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsPathOnReadOnlyMount()
    {
        var validator = CreateValidator(hostRoot: _root, new MountInfo(_parent, "ext4", IsReadOnly: true));

        var ok = validator.TryValidateForDelete(_root, Path.Combine(_root, "sub", "child.txt"), out _, out var error);

        Assert.False(ok);
        Assert.Contains("read-only", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsWhenNoMountContainsThePath()
    {
        var validator = CreateValidator(hostRoot: _root);

        var ok = validator.TryValidateForDelete(_root, Path.Combine(_root, "sub", "child.txt"), out _, out var error);

        Assert.False(ok);
        Assert.Contains("read-only", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsesInnermostMountForReadOnlyFlag()
    {
        var rwInsideRo = CreateValidator(hostRoot: _root,
            new MountInfo(_parent, "ext4", IsReadOnly: true),
            new MountInfo(_root, "ext4", IsReadOnly: false));
        Assert.True(rwInsideRo.TryValidateForDelete(_root, Path.Combine(_root, "sub", "child.txt"), out _, out var error), error);

        var roInsideRw = CreateValidator(hostRoot: _root,
            new MountInfo(_parent, "ext4", IsReadOnly: false),
            new MountInfo(Path.Combine(_root, "sub"), "ext4", IsReadOnly: true));
        Assert.False(roInsideRw.TryValidateForDelete(_root, Path.Combine(_root, "sub", "child.txt"), out _, out error));
        Assert.Contains("read-only", error, StringComparison.OrdinalIgnoreCase);
        Assert.True(roInsideRw.TryValidateForDelete(_root, Path.Combine(_root, "top.txt"), out _, out error), error);
    }

    private sealed class FakeMountTable(IReadOnlyList<MountInfo> mounts) : MountTableBase
    {
        public override IReadOnlyList<MountInfo> GetMounts() => mounts;
    }
}
