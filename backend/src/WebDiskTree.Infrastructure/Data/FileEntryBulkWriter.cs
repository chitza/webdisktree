using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Data;

/// <summary>
/// Bulk-inserts flattened scan rows via raw ADO.NET in a single transaction. EF Core's change-tracked
/// SaveChanges does not scale to the millions of rows a full disk scan can produce; this bypasses it entirely.
/// </summary>
public class FileEntryBulkWriter(WebDiskTreeDbContext dbContext)
{
    public async Task WriteAsync(Guid scanId, IEnumerable<FlatFileRow> rows, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO FileEntries (ScanId, ParentPath, Name, Extension, SizeBytes, ModifiedUtc, IsDirectory)
            VALUES ($scanId, $parentPath, $name, $extension, $sizeBytes, $modifiedUtc, $isDirectory);
            """;

        var scanIdParam = command.CreateParameter();
        scanIdParam.ParameterName = "$scanId";
        command.Parameters.Add(scanIdParam);

        var parentPathParam = command.CreateParameter();
        parentPathParam.ParameterName = "$parentPath";
        command.Parameters.Add(parentPathParam);

        var nameParam = command.CreateParameter();
        nameParam.ParameterName = "$name";
        command.Parameters.Add(nameParam);

        var extensionParam = command.CreateParameter();
        extensionParam.ParameterName = "$extension";
        command.Parameters.Add(extensionParam);

        var sizeBytesParam = command.CreateParameter();
        sizeBytesParam.ParameterName = "$sizeBytes";
        command.Parameters.Add(sizeBytesParam);

        var modifiedUtcParam = command.CreateParameter();
        modifiedUtcParam.ParameterName = "$modifiedUtc";
        command.Parameters.Add(modifiedUtcParam);

        var isDirectoryParam = command.CreateParameter();
        isDirectoryParam.ParameterName = "$isDirectory";
        command.Parameters.Add(isDirectoryParam);

        command.Prepare();

        // EF Core's default Sqlite Guid-to-TEXT conversion stores uppercase "D"-format strings; matching that
        // here is required so EF-side WHERE ScanId == @guid queries (case-sensitive TEXT comparison) find these
        // raw-inserted rows.
        scanIdParam.Value = scanId.ToString("D").ToUpperInvariant();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            parentPathParam.Value = row.ParentPath;
            nameParam.Value = row.Name;
            extensionParam.Value = (object?)row.Extension ?? DBNull.Value;
            sizeBytesParam.Value = row.SizeBytes;
            modifiedUtcParam.Value = row.ModifiedUtc.ToUnixTimeMilliseconds();
            isDirectoryParam.Value = row.IsDirectory;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Removes the row for <paramref name="canonicalPath"/> itself, plus (if it was a directory) every
    /// descendant row, so the list view/breakdown reflect the deletion immediately without waiting for a rescan.</summary>
    public async Task DeleteTreeAsync(Guid scanId, string canonicalPath, bool isDirectory, CancellationToken cancellationToken)
    {
        var parentPath = Path.GetDirectoryName(canonicalPath) ?? string.Empty;
        var name = Path.GetFileName(canonicalPath);

        await dbContext.FileEntries
            .Where(f => f.ScanId == scanId && f.ParentPath == parentPath && f.Name == name)
            .ExecuteDeleteAsync(cancellationToken);

        if (isDirectory)
        {
            var descendantPrefix = canonicalPath + "/";
            await dbContext.FileEntries
                .Where(f => f.ScanId == scanId && (f.ParentPath == canonicalPath || f.ParentPath.StartsWith(descendantPrefix)))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
