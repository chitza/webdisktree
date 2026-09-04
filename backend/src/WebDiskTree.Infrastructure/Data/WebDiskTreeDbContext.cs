using Microsoft.EntityFrameworkCore;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Infrastructure.Data;

public class WebDiskTreeDbContext(DbContextOptions<WebDiskTreeDbContext> options) : DbContext(options)
{
    public DbSet<ScanEntity> Scans => Set<ScanEntity>();
    public DbSet<ScheduleEntity> Schedules => Set<ScheduleEntity>();
    public DbSet<FileEntryEntity> FileEntries => Set<FileEntryEntity>();
    public DbSet<DirectoryPathEntity> DirectoryPaths => Set<DirectoryPathEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScanEntity>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.StartedAt);
        });

        modelBuilder.Entity<ScheduleEntity>(b =>
        {
            b.HasKey(s => s.Id);
        });

        modelBuilder.Entity<DirectoryPathEntity>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.ScanId, d.Path }).IsUnique();
        });

        modelBuilder.Entity<FileEntryEntity>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.Id).ValueGeneratedOnAdd();
            // Stored as unix-ms long, not EF's default ISO-8601 text, so the raw ADO.NET bulk-insert path
            // (FileEntryBulkWriter) and EF's LINQ read path agree on wire format without going through EF writes.
            b.Property(f => f.ModifiedUtc).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            b.HasIndex(f => new { f.ScanId, f.ParentDirectoryId });
            b.HasIndex(f => new { f.ScanId, f.Extension });
            // No index on SizeBytes: nothing queries FileEntries by size alone (GetFiles sorts an
            // already-ParentDirectoryId-filtered result in memory), so it was pure dead weight.
        });
    }
}
