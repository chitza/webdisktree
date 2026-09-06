using WebDiskTree.Core.Models;

namespace WebDiskTree.Api.Dtos;

public record CreateScanRequest(string RootPath);

public record SetScanPinnedRequest(bool IsPinned);

public record ScanSummaryDto(
    Guid Id,
    string RootPath,
    ScanTrigger Trigger,
    ScanStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long TotalBytes,
    long TotalFiles,
    long TotalDirs,
    int ErrorCount,
    bool IsStale,
    string? ErrorMessage,
    bool IsPinned = false);

public record FileEntryDto(
    string Name,
    string? Extension,
    long SizeBytes,
    DateTimeOffset ModifiedUtc,
    bool IsDirectory,
    string? ParsedTitle = null,
    string? ImdbUrl = null,
    ImdbLookupStatus? ImdbStatus = null);

public record TriggerImdbLookupRequest(string Path);

public record TriggerImdbLookupResult(int Queued, int AlreadyCached);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

public record TypeBreakdownEntryDto(string Extension, long TotalSizeBytes, long FileCount);

public record DeleteFilesRequest(Guid ScanId, IReadOnlyList<string> Paths);

public record DeleteFilesResult(IReadOnlyList<string> Deleted, IReadOnlyList<DeleteFailureDto> Failed);

public record DeleteFailureDto(string Path, string Reason);

public record CreateScheduleRequest(string RootPath, string CronExpression, bool Enabled);

public record ScheduleDto(
    Guid Id,
    string RootPath,
    string CronExpression,
    bool Enabled,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

public record AllowedRootDto(string Path, string Label, bool AllowDelete);

public record ImdbLookupCacheSummaryDto(int Count);

public record ImdbLookupCacheRow(
    string CacheKey,
    string ParsedTitle,
    int? Year,
    MediaKind Kind,
    string? ImdbId,
    ImdbLookupStatus Status,
    DateTimeOffset? LastAttemptAt);

public record ImdbLookupCacheImportResult(int Added, int Updated);
