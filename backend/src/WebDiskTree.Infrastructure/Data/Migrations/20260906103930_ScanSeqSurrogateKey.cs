using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDiskTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    // Hand-written instead of the scaffolded version: the scaffolder's default column-add + table-rebuild
    // copies ScanSeq's DEFAULT 0 placeholder value verbatim into the rebuilt table, discarding every row's
    // real scan association. This backfills ScanSeq from the pre-existing ScanId (matched against the
    // now-finalized Scans.SeqId) before that column is dropped.
    public partial class ScanSeqSurrogateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_FileEntries_ScanId_Extension\";");
            migrationBuilder.Sql("DROP INDEX \"IX_FileEntries_ScanId_ParentDirectoryId\";");
            migrationBuilder.Sql("DROP INDEX \"IX_DirectoryPaths_ScanId_Path\";");

            // Rebuild Scans with SeqId as the real autoincrement primary key and Id kept as a unique
            // alternate key. Only a handful of rows exist today, so rebuilding this table is cheap.
            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_Scans" (
                    "SeqId" INTEGER NOT NULL CONSTRAINT "PK_Scans" PRIMARY KEY AUTOINCREMENT,
                    "BlobPath" TEXT NULL,
                    "CompletedAt" TEXT NULL,
                    "ErrorCount" INTEGER NOT NULL,
                    "ErrorMessage" TEXT NULL,
                    "Id" TEXT NOT NULL,
                    "IsPinned" INTEGER NOT NULL,
                    "IsStale" INTEGER NOT NULL,
                    "RootPath" TEXT NOT NULL,
                    "StartedAt" TEXT NULL,
                    "Status" INTEGER NOT NULL,
                    "TotalBytes" INTEGER NOT NULL,
                    "TotalDirs" INTEGER NOT NULL,
                    "TotalFiles" INTEGER NOT NULL,
                    "Trigger" INTEGER NOT NULL,
                    CONSTRAINT "AK_Scans_Id" UNIQUE ("Id")
                );
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "ef_temp_Scans" ("BlobPath", "CompletedAt", "ErrorCount", "ErrorMessage", "Id", "IsPinned", "IsStale", "RootPath", "StartedAt", "Status", "TotalBytes", "TotalDirs", "TotalFiles", "Trigger")
                SELECT "BlobPath", "CompletedAt", "ErrorCount", "ErrorMessage", "Id", "IsPinned", "IsStale", "RootPath", "StartedAt", "Status", "TotalBytes", "TotalDirs", "TotalFiles", "Trigger"
                FROM "Scans" ORDER BY rowid;
                """);
            migrationBuilder.Sql("DROP TABLE \"Scans\";");
            migrationBuilder.Sql("ALTER TABLE \"ef_temp_Scans\" RENAME TO \"Scans\";");
            migrationBuilder.Sql("CREATE INDEX \"IX_Scans_StartedAt\" ON \"Scans\" (\"StartedAt\");");

            // Backfill ScanSeq from the old ScanId (still present on these tables) before dropping it.
            migrationBuilder.Sql("ALTER TABLE \"FileEntries\" ADD COLUMN \"ScanSeq\" INTEGER NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(
                "UPDATE \"FileEntries\" SET \"ScanSeq\" = (SELECT \"SeqId\" FROM \"Scans\" WHERE \"Scans\".\"Id\" = \"FileEntries\".\"ScanId\");");
            migrationBuilder.Sql("ALTER TABLE \"FileEntries\" DROP COLUMN \"ScanId\";");

            migrationBuilder.Sql("ALTER TABLE \"DirectoryPaths\" ADD COLUMN \"ScanSeq\" INTEGER NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(
                "UPDATE \"DirectoryPaths\" SET \"ScanSeq\" = (SELECT \"SeqId\" FROM \"Scans\" WHERE \"Scans\".\"Id\" = \"DirectoryPaths\".\"ScanId\");");
            migrationBuilder.Sql("ALTER TABLE \"DirectoryPaths\" DROP COLUMN \"ScanId\";");

            migrationBuilder.Sql(
                "CREATE INDEX \"IX_FileEntries_ScanSeq_Extension\" ON \"FileEntries\" (\"ScanSeq\", \"Extension\");");
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_FileEntries_ScanSeq_ParentDirectoryId\" ON \"FileEntries\" (\"ScanSeq\", \"ParentDirectoryId\");");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_DirectoryPaths_ScanSeq_Path\" ON \"DirectoryPaths\" (\"ScanSeq\", \"Path\");");

            // Switches to incremental auto-vacuum (a plain VACUUM run afterward, not on every write like FULL
            // would be) so scan deletion can cheaply reclaim freed pages via PRAGMA incremental_vacuum instead
            // of leaving them in the freelist forever. Also reclaims the freelist bloat already sitting in the
            // file from before this database used auto-vacuum at all. Runs outside the migration's transaction
            // because SQLite refuses to VACUUM inside one.
            migrationBuilder.Sql("PRAGMA auto_vacuum = INCREMENTAL;", suppressTransaction: true);
            migrationBuilder.Sql("VACUUM;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_FileEntries_ScanSeq_Extension\";");
            migrationBuilder.Sql("DROP INDEX \"IX_FileEntries_ScanSeq_ParentDirectoryId\";");
            migrationBuilder.Sql("DROP INDEX \"IX_DirectoryPaths_ScanSeq_Path\";");

            migrationBuilder.Sql("ALTER TABLE \"FileEntries\" ADD COLUMN \"ScanId\" TEXT NOT NULL DEFAULT '';");
            migrationBuilder.Sql(
                "UPDATE \"FileEntries\" SET \"ScanId\" = (SELECT \"Id\" FROM \"Scans\" WHERE \"Scans\".\"SeqId\" = \"FileEntries\".\"ScanSeq\");");
            migrationBuilder.Sql("ALTER TABLE \"FileEntries\" DROP COLUMN \"ScanSeq\";");

            migrationBuilder.Sql("ALTER TABLE \"DirectoryPaths\" ADD COLUMN \"ScanId\" TEXT NOT NULL DEFAULT '';");
            migrationBuilder.Sql(
                "UPDATE \"DirectoryPaths\" SET \"ScanId\" = (SELECT \"Id\" FROM \"Scans\" WHERE \"Scans\".\"SeqId\" = \"DirectoryPaths\".\"ScanSeq\");");
            migrationBuilder.Sql("ALTER TABLE \"DirectoryPaths\" DROP COLUMN \"ScanSeq\";");

            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_Scans" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Scans" PRIMARY KEY,
                    "RootPath" TEXT NOT NULL,
                    "Trigger" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "StartedAt" TEXT NULL,
                    "CompletedAt" TEXT NULL,
                    "TotalBytes" INTEGER NOT NULL,
                    "TotalFiles" INTEGER NOT NULL,
                    "TotalDirs" INTEGER NOT NULL,
                    "ErrorCount" INTEGER NOT NULL,
                    "BlobPath" TEXT NULL,
                    "IsStale" INTEGER NOT NULL,
                    "ErrorMessage" TEXT NULL,
                    "IsPinned" INTEGER NOT NULL DEFAULT 0
                );
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "ef_temp_Scans" ("Id", "RootPath", "Trigger", "Status", "StartedAt", "CompletedAt", "TotalBytes", "TotalFiles", "TotalDirs", "ErrorCount", "BlobPath", "IsStale", "ErrorMessage", "IsPinned")
                SELECT "Id", "RootPath", "Trigger", "Status", "StartedAt", "CompletedAt", "TotalBytes", "TotalFiles", "TotalDirs", "ErrorCount", "BlobPath", "IsStale", "ErrorMessage", "IsPinned"
                FROM "Scans" ORDER BY "SeqId";
                """);
            migrationBuilder.Sql("DROP TABLE \"Scans\";");
            migrationBuilder.Sql("ALTER TABLE \"ef_temp_Scans\" RENAME TO \"Scans\";");
            migrationBuilder.Sql("CREATE INDEX \"IX_Scans_StartedAt\" ON \"Scans\" (\"StartedAt\");");

            migrationBuilder.Sql(
                "CREATE INDEX \"IX_FileEntries_ScanId_Extension\" ON \"FileEntries\" (\"ScanId\", \"Extension\");");
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_FileEntries_ScanId_ParentDirectoryId\" ON \"FileEntries\" (\"ScanId\", \"ParentDirectoryId\");");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_DirectoryPaths_ScanId_Path\" ON \"DirectoryPaths\" (\"ScanId\", \"Path\");");
        }
    }
}
