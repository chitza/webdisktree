using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Data;

/// <summary>
/// Bulk-inserts flattened scan rows via raw ADO.NET, committing in batches. EF Core's change-tracked
/// SaveChanges does not scale to the millions of rows a full disk scan can produce; this bypasses it entirely.
/// </summary>
public class FileEntryBulkWriter(WebDiskTreeDbContext dbContext)
{
    // Bounds how much a single scan can hold open in one uncommitted transaction. A scan of millions of rows
    // committed as one transaction lets SQLite's WAL grow unboundedly for its entire duration (checkpoints only
    // happen at commit boundaries), which then starves every concurrent read until that one commit finally
    // lands. Batching gives SQLite a checkpoint opportunity every ~50k rows instead of once at the very end.
    private const int BatchSize = 50_000;

    public async Task WriteAsync(int scanSeq, IEnumerable<FlatFileRow> rows, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        // Interns each distinct parent path once per scan (a directory with 1,000 files hits this once, not
        // 1,000 times) instead of writing/indexing the full path string on every file row. Persists across
        // batches — it's scoped to the whole scan, not any one transaction.
        var pathToDirectoryId = new Dictionary<string, long>();

        using var enumerator = rows.GetEnumerator();
        var hasNext = enumerator.MoveNext();

        while (hasNext)
        {
            using var transaction = connection.BeginTransaction();
            using var insertDirCommand = CreateInsertDirectoryCommand(connection, transaction, scanSeq);
            using var lastRowIdCommand = CreateLastRowIdCommand(connection, transaction);
            using var insertFileCommand = CreateInsertFileCommand(connection, transaction, scanSeq, out var fileParams);

            var insertDirPathParam = insertDirCommand.Parameters["$path"];

            for (var i = 0; i < BatchSize && hasNext; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = enumerator.Current;

                if (!pathToDirectoryId.TryGetValue(row.ParentPath, out var directoryId))
                {
                    insertDirPathParam.Value = row.ParentPath;
                    await insertDirCommand.ExecuteNonQueryAsync(cancellationToken);
                    directoryId = (long)(await lastRowIdCommand.ExecuteScalarAsync(cancellationToken))!;
                    pathToDirectoryId[row.ParentPath] = directoryId;
                }

                fileParams.ParentDirectoryId.Value = directoryId;
                fileParams.Name.Value = row.Name;
                fileParams.Extension.Value = (object?)row.Extension ?? DBNull.Value;
                fileParams.SizeBytes.Value = row.SizeBytes;
                fileParams.ModifiedUtc.Value = row.ModifiedUtc.ToUnixTimeMilliseconds();
                fileParams.IsDirectory.Value = row.IsDirectory;
                await insertFileCommand.ExecuteNonQueryAsync(cancellationToken);

                hasNext = enumerator.MoveNext();
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static SqliteCommand CreateInsertDirectoryCommand(SqliteConnection connection, SqliteTransaction transaction, int scanSeq)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO DirectoryPaths (ScanSeq, Path) VALUES ($scanSeq, $path);";
        command.Parameters.AddWithValue("$scanSeq", scanSeq);
        command.Parameters.Add("$path", SqliteType.Text);
        command.Prepare();
        return command;
    }

    private static SqliteCommand CreateLastRowIdCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";
        command.Prepare();
        return command;
    }

    private static SqliteCommand CreateInsertFileCommand(
        SqliteConnection connection, SqliteTransaction transaction, int scanSeq, out FileInsertParameters fileParams)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO FileEntries (ScanSeq, ParentDirectoryId, Name, Extension, SizeBytes, ModifiedUtc, IsDirectory)
            VALUES ($scanSeq, $parentDirectoryId, $name, $extension, $sizeBytes, $modifiedUtc, $isDirectory);
            """;
        command.Parameters.AddWithValue("$scanSeq", scanSeq);

        var parentDirectoryId = command.Parameters.Add("$parentDirectoryId", SqliteType.Integer);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var extension = command.Parameters.Add("$extension", SqliteType.Text);
        var sizeBytes = command.Parameters.Add("$sizeBytes", SqliteType.Integer);
        var modifiedUtc = command.Parameters.Add("$modifiedUtc", SqliteType.Integer);
        var isDirectory = command.Parameters.Add("$isDirectory", SqliteType.Integer);
        command.Prepare();

        fileParams = new FileInsertParameters(parentDirectoryId, name, extension, sizeBytes, modifiedUtc, isDirectory);
        return command;
    }

    private readonly record struct FileInsertParameters(
        SqliteParameter ParentDirectoryId,
        SqliteParameter Name,
        SqliteParameter Extension,
        SqliteParameter SizeBytes,
        SqliteParameter ModifiedUtc,
        SqliteParameter IsDirectory);

    /// <summary>Removes the row for <paramref name="canonicalPath"/> itself, plus (if it was a directory) every
    /// descendant row, so the list view/breakdown reflect the deletion immediately without waiting for a rescan.</summary>
    public async Task DeleteTreeAsync(int scanSeq, string canonicalPath, bool isDirectory, CancellationToken cancellationToken)
    {
        var parentPath = Path.GetDirectoryName(canonicalPath) ?? string.Empty;
        var name = Path.GetFileName(canonicalPath);

        var parentDirectoryId = await dbContext.DirectoryPaths
            .Where(d => d.ScanSeq == scanSeq && d.Path == parentPath)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (parentDirectoryId is not null)
        {
            await dbContext.FileEntries
                .Where(f => f.ScanSeq == scanSeq && f.ParentDirectoryId == parentDirectoryId && f.Name == name)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (isDirectory)
        {
            // Runs against DirectoryPaths (one row per unique directory) rather than FileEntries (one row per
            // file), so this prefix scan is cheap even though it can no longer use an equality index lookup.
            var descendantPrefix = canonicalPath + "/";
            var descendantDirectoryIds = await dbContext.DirectoryPaths
                .Where(d => d.ScanSeq == scanSeq && (d.Path == canonicalPath || d.Path.StartsWith(descendantPrefix)))
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (descendantDirectoryIds.Count > 0)
            {
                await dbContext.FileEntries
                    .Where(f => f.ScanSeq == scanSeq && descendantDirectoryIds.Contains(f.ParentDirectoryId))
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.DirectoryPaths
                    .Where(d => descendantDirectoryIds.Contains(d.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
    }
}
