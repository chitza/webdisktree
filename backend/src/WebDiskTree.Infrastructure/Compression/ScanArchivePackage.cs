using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;

namespace WebDiskTree.Infrastructure.Compression;

public static class ScanArchivePackage
{
    public const long MaxExpandedBytes = 1024L * 1024 * 1024;

    private static FileStream TemporaryStream() => new(
        Path.Combine(Path.GetTempPath(), $"webdisktree-{Guid.NewGuid()}.tmp"),
        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
        FileOptions.Asynchronous | FileOptions.DeleteOnClose);

    public static async Task<Stream> WriteAsync<T>(T archive, CancellationToken cancellationToken)
    {
        // Tar needs the entry length in advance. Spool to disk instead of retaining another
        // full JSON copy in memory alongside the tree and file listings.
        await using var json = TemporaryStream();
        await JsonSerializer.SerializeAsync(json, archive, TreeBlobSerializer.ArchiveJsonOptions, cancellationToken);
        json.Position = 0;
        var package = TemporaryStream();
        try
        {
            await using (var gzip = new GZipStream(package, CompressionLevel.Fastest, leaveOpen: true))
            await using (var tar = new TarWriter(gzip, leaveOpen: true))
            {
                await tar.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "scan.json")
                {
                    DataStream = json,
                }, cancellationToken);
            }
            package.Position = 0;
            return package;
        }
        catch
        {
            await package.DisposeAsync();
            throw;
        }
    }

    public static async Task<T?> ReadAsync<T>(Stream source, CancellationToken cancellationToken)
    {
        using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        using var tar = new TarReader(gzip, leaveOpen: true);
        var entry = await tar.GetNextEntryAsync(cancellationToken: cancellationToken);
        if (entry is null || entry.Name != "scan.json" || entry.EntryType != TarEntryType.RegularFile
            || entry.DataStream is null || entry.Length > MaxExpandedBytes)
            throw new InvalidDataException("Expected scan.json (up to 1 GiB) in the scan archive.");

        // Read only the expected entry; never extract archive paths onto the filesystem.
        return await JsonSerializer.DeserializeAsync<T>(entry.DataStream,
            TreeBlobSerializer.ArchiveJsonOptions, cancellationToken);
    }
}
