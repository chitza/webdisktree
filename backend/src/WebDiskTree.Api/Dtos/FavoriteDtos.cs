namespace WebDiskTree.Api.Dtos;

public record FavoriteDto(Guid Id, string Path, string Label, DateTimeOffset CreatedAt);

public record CreateFavoriteRequest(string Path, string? Label);

public record UpdateFavoriteRequest(string Label);
