using Cronos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDiskTree.Api.Dtos;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Data.Entities;
using WebDiskTree.Infrastructure.Scanning;
using WebDiskTree.Infrastructure.Security;

namespace WebDiskTree.Api.Controllers;

[ApiController]
[Route("api/schedules")]
public class SchedulesController(
    WebDiskTreeDbContext dbContext,
    ScanQueue queue,
    AllowedRootsService allowedRoots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetSchedules(CancellationToken cancellationToken)
    {
        var schedules = await dbContext.Schedules.OrderBy(s => s.RootPath).ToListAsync(cancellationToken);
        return Ok(schedules.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!allowedRoots.IsAllowed(request.RootPath))
        {
            return BadRequest("RootPath is not under any configured allowed root.");
        }

        if (!TryParseCron(request.CronExpression, out var error))
        {
            return BadRequest(error);
        }

        var entity = new ScheduleEntity
        {
            Id = Guid.NewGuid(),
            RootPath = Path.GetFullPath(request.RootPath),
            CronExpression = request.CronExpression,
            Enabled = request.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Schedules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSchedules), new { }, ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!allowedRoots.IsAllowed(request.RootPath))
        {
            return BadRequest("RootPath is not under any configured allowed root.");
        }

        if (!TryParseCron(request.CronExpression, out var error))
        {
            return BadRequest(error);
        }

        entity.RootPath = Path.GetFullPath(request.RootPath);
        entity.CronExpression = request.CronExpression;
        entity.Enabled = request.Enabled;
        entity.NextRunAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        dbContext.Schedules.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/run-now")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (schedule is null)
        {
            return NotFound();
        }

        var scanId = Guid.NewGuid();
        dbContext.Scans.Add(new ScanEntity
        {
            Id = scanId,
            RootPath = schedule.RootPath,
            Trigger = ScanTrigger.Scheduled,
            Status = ScanStatus.Pending,
        });
        schedule.LastRunAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        queue.Enqueue(new ScanJobRequest(scanId, schedule.RootPath, ScanTrigger.Scheduled));

        return Accepted(new { scanId });
    }

    private static bool TryParseCron(string cronExpression, out string? error)
    {
        try
        {
            CronExpression.Parse(cronExpression);
            error = null;
            return true;
        }
        catch (CronFormatException ex)
        {
            error = $"Invalid cron expression: {ex.Message}";
            return false;
        }
    }

    private static ScheduleDto ToDto(ScheduleEntity s) => new(s.Id, s.RootPath, s.CronExpression, s.Enabled, s.LastRunAt, s.NextRunAt);
}
