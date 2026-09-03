using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Scanning;

namespace WebDiskTree.Infrastructure.Scheduling;

/// <summary>
/// Polls enabled schedules every minute and enqueues due ones onto the same <see cref="ScanQueue"/> manual
/// scans use, so scheduled and manual scans share one pipeline with no duplicate code path.
/// </summary>
public class ScheduleEvaluatorService(
    IServiceScopeFactory scopeFactory,
    ScanQueue queue,
    ILogger<ScheduleEvaluatorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await EvaluateDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Schedule evaluation pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EvaluateDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebDiskTreeDbContext>();

        var now = DateTimeOffset.UtcNow;
        var schedules = await dbContext.Schedules.Where(s => s.Enabled).ToListAsync(cancellationToken);

        foreach (var schedule in schedules)
        {
            CronExpression cron;
            try
            {
                cron = CronExpression.Parse(schedule.CronExpression);
            }
            catch (CronFormatException ex)
            {
                logger.LogWarning(ex, "Schedule {ScheduleId} has an invalid cron expression '{Cron}'; skipping", schedule.Id, schedule.CronExpression);
                continue;
            }

            schedule.NextRunAt ??= cron.GetNextOccurrence(schedule.LastRunAt?.UtcDateTime ?? now.UtcDateTime, TimeZoneInfo.Utc);

            if (schedule.NextRunAt is { } nextRun && nextRun <= now)
            {
                var scanId = Guid.NewGuid();
                dbContext.Scans.Add(new Data.Entities.ScanEntity
                {
                    Id = scanId,
                    RootPath = schedule.RootPath,
                    Trigger = ScanTrigger.Scheduled,
                    Status = ScanStatus.Pending,
                });

                schedule.LastRunAt = now;
                schedule.NextRunAt = cron.GetNextOccurrence(now.UtcDateTime, TimeZoneInfo.Utc);

                await dbContext.SaveChangesAsync(cancellationToken);
                queue.Enqueue(new ScanJobRequest(scanId, schedule.RootPath, ScanTrigger.Scheduled));
            }
        }
    }
}
