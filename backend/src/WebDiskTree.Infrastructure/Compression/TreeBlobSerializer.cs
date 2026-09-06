using System.IO.Compression;
using System.Text.Json;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Compression;

public class TreeBlobSerializer
{
    // Each directory adds an object and a children array to the JSON depth.
    // Reserve one extra level in archives for the envelope surrounding the tree.
    public const int MaxTreeJsonDepth = 1024;
    public static JsonSerializerOptions ArchiveJsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = MaxTreeJsonDepth + 1,
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = MaxTreeJsonDepth,
    };

    public async Task WriteAsync(string blobPath, DirectoryNode root, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
        await using var fileStream = File.Create(blobPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest);
        await JsonSerializer.SerializeAsync(gzipStream, root, JsonOptions, cancellationToken);
    }

    public async Task<DirectoryNode?> ReadAsync(string blobPath, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(blobPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<DirectoryNode>(gzipStream, JsonOptions, cancellationToken);
    }
}
