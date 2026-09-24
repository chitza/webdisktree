namespace WebDiskTree.Infrastructure.Data.Entities;

public class FavoritePathEntity
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
    public required string Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
