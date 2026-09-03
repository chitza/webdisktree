using Microsoft.EntityFrameworkCore;
using WebDiskTree.Infrastructure.Data.Entities;

namespace WebDiskTree.Infrastructure.Data;

public class WebDiskTreeDbContext(DbContextOptions<WebDiskTreeDbContext> options) : DbContext(options)
{
    public DbSet<ScanEntity> Scans => Set<ScanEntity>();
    public DbSet<ScheduleEntity> Schedules => Set<ScheduleEntity>();
    public DbSet<FileEntryEntity> FileEntries => Set<FileEntryEntity>();

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

        modelBuilder.Entity<FileEntryEntity>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.Id).ValueGeneratedOnAdd();
            // Stored as unix-ms long, not EF's default ISO-8601 text, so the raw ADO.NET bulk-insert path
            // (FileEntryBulkWriter) and EF's LINQ read path agree on wire format without going through EF writes.
            b.Property(f => f.ModifiedUtc).HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            b.HasIndex(f => new { f.ScanId, f.ParentPath });
            b.HasIndex(f => new { f.ScanId, f.Extension });
            b.HasIndex(f => new { f.ScanId, f.SizeBytes });
        });
    }
}
