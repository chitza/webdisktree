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

        // EF Core's default Sqlite Guid-to-TEXT conversion stores uppercase "D"-format strings; matching that
        // here is required so EF-side WHERE ScanId == @guid queries (case-sensitive TEXT comparison) find these
        // raw-inserted rows.
        var scanIdText = scanId.ToString("D").ToUpperInvariant();

        using var insertDirCommand = connection.CreateCommand();
        insertDirCommand.Transaction = transaction;
        insertDirCommand.CommandText =
            """
            INSERT INTO DirectoryPaths (ScanId, Path) VALUES ($scanId, $path);
            """;
        var insertDirScanIdParam = insertDirCommand.CreateParameter();
        insertDirScanIdParam.ParameterName = "$scanId";
        insertDirScanIdParam.Value = scanIdText;
        insertDirCommand.Parameters.Add(insertDirScanIdParam);
        var insertDirPathParam = insertDirCommand.CreateParameter();
        insertDirPathParam.ParameterName = "$path";
        insertDirCommand.Parameters.Add(insertDirPathParam);
        insertDirCommand.Prepare();

        using var lastRowIdCommand = connection.CreateCommand();
        lastRowIdCommand.Transaction = transaction;
        lastRowIdCommand.CommandText = "SELECT last_insert_rowid();";
        lastRowIdCommand.Prepare();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO FileEntries (ScanId, ParentDirectoryId, Name, Extension, SizeBytes, ModifiedUtc, IsDirectory)
            VALUES ($scanId, $parentDirectoryId, $name, $extension, $sizeBytes, $modifiedUtc, $isDirectory);
            """;

        var scanIdParam = command.CreateParameter();
        scanIdParam.ParameterName = "$scanId";
        scanIdParam.Value = scanIdText;
        command.Parameters.Add(scanIdParam);

        var parentDirectoryIdParam = command.CreateParameter();
        parentDirectoryIdParam.ParameterName = "$parentDirectoryId";
        command.Parameters.Add(parentDirectoryIdParam);

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

        // Interns each distinct parent path once per scan (a directory with 1,000 files hits this once, not
        // 1,000 times) instead of writing/indexing the full path string on every file row.
        var pathToDirectoryId = new Dictionary<string, long>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pathToDirectoryId.TryGetValue(row.ParentPath, out var directoryId))
            {
                insertDirPathParam.Value = row.ParentPath;
                await insertDirCommand.ExecuteNonQueryAsync(cancellationToken);
                directoryId = (long)(await lastRowIdCommand.ExecuteScalarAsync(cancellationToken))!;
                pathToDirectoryId[row.ParentPath] = directoryId;
            }

            parentDirectoryIdParam.Value = directoryId;
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

        var parentDirectoryId = await dbContext.DirectoryPaths
            .Where(d => d.ScanId == scanId && d.Path == parentPath)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (parentDirectoryId is not null)
        {
            await dbContext.FileEntries
                .Where(f => f.ScanId == scanId && f.ParentDirectoryId == parentDirectoryId && f.Name == name)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (isDirectory)
        {
            // Runs against DirectoryPaths (one row per unique directory) rather than FileEntries (one row per
            // file), so this prefix scan is cheap even though it can no longer use an equality index lookup.
            var descendantPrefix = canonicalPath + "/";
            var descendantDirectoryIds = await dbContext.DirectoryPaths
                .Where(d => d.ScanId == scanId && (d.Path == canonicalPath || d.Path.StartsWith(descendantPrefix)))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (descendantDirectoryIds.Count > 0)
            {
                await dbContext.FileEntries
                    .Where(f => f.ScanId == scanId && descendantDirectoryIds.Contains(f.ParentDirectoryId))
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.DirectoryPaths
                    .Where(d => descendantDirectoryIds.Contains(d.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
    }
}
