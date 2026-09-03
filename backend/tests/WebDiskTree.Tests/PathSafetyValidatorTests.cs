using Microsoft.Extensions.Options;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Tests;

public class PathSafetyValidatorTests : IDisposable
{
    private readonly string _root;
    private readonly PathSafetyValidator _validator;

    public PathSafetyValidatorTests()
    {
        _root = Directory.CreateTempSubdirectory("webdisktree-safety-test-").FullName;
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "child.txt"), "x");

        var options = Options.Create(new AllowedRootsOptions
        {
            Roots = [new AllowedRoot { Path = _root, Label = "test", AllowDelete = true }],
        });
        _validator = new PathSafetyValidator(options);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

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
    public void AllowsValidDescendantUnderAllowedRoot()
    {
        var childPath = Path.Combine(_root, "sub", "child.txt");
        var ok = _validator.TryValidateForDelete(_root, childPath, out var canonical, out var error);
        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(childPath), canonical);
    }

    [Fact]
    public void RejectsWhenRootNotInAllowList()
    {
        var otherRoot = Directory.CreateTempSubdirectory("webdisktree-other-root-").FullName;
        try
        {
            var childPath = Path.Combine(otherRoot, "file.txt");
            File.WriteAllText(childPath, "x");

            var ok = _validator.TryValidateForDelete(otherRoot, childPath, out _, out var error);
            Assert.False(ok);
            Assert.Contains("allowed", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(otherRoot, recursive: true);
        }
    }
}
