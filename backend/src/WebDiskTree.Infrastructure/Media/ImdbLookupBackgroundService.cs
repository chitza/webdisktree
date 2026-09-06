using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebDiskTree.Core.Models;
using WebDiskTree.Infrastructure.Data;

namespace WebDiskTree.Infrastructure.Media;

/// <summary>Dequeues IMDB lookup requests (enqueued by FilesController when the user clicks "Find IMDB
/// links") and resolves them one at a time via TmdbClient, mirroring ScanBackgroundService's pattern.</summary>
public class ImdbLookupBackgroundService(
    ImdbLookupQueue queue,
    TmdbClient tmdbClient,
    IServiceScopeFactory scopeFactory,
    ILogger<ImdbLookupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(request, stoppingToken);
        }
    }

    private async Task ProcessAsync(ImdbLookupRequest request, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebDiskTreeDbContext>();

        var cacheEntry = await dbContext.ImdbLookupCache
            .FirstOrDefaultAsync(c => c.CacheKey == request.CacheKey, stoppingToken);
        if (cacheEntry is null)
        {
            logger.LogWarning("Imdb lookup cache row for {CacheKey} not found; skipping", request.CacheKey);
            return;
        }

        try
        {
            var imdbId = await tmdbClient.FindImdbIdAsync(request.Title, request.Year, request.Kind, stoppingToken);
            cacheEntry.ImdbId = imdbId;
            cacheEntry.Status = imdbId is null ? ImdbLookupStatus.NotFound : ImdbLookupStatus.Found;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Imdb lookup failed for {Title} ({Year})", request.Title, request.Year);
            cacheEntry.Status = ImdbLookupStatus.Failed;
        }

        cacheEntry.LastAttemptAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
