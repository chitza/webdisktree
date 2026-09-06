namespace WebDiskTree.Infrastructure.Media;

/// <summary>Bound from configuration ("Tmdb" section). Leave ApiKey empty to disable IMDB lookups entirely
/// (TmdbClient no-ops rather than failing) — supply a real key via user-secrets or the Tmdb__ApiKey env var,
/// never by editing the checked-in appsettings files.</summary>
public class TmdbOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";
}
