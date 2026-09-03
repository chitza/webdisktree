using System.IO.Compression;
using System.Text.Json;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Compression;

public class TreeBlobSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
