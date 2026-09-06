using System.Text;
using System.Text.RegularExpressions;

namespace WebDiskTree.Core.Models;

/// <summary>
/// Turns a release-style video filename (e.g. "There.Will.Be.Blood.2007.REPACK.720p.BluRay.x264-DON.mkv")
/// into a clean title suitable for searching an external metadata API. Pure/no I/O so it's cheap to call
/// per-row on every file list request.
/// </summary>
public static class MediaTitleParser
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".ts", ".webm", ".flv",
    };

    private static readonly Regex EpisodeRegex = new(@"\bS\d{1,2}E\d{1,3}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Covers both "Season 1" and the far more common release-style bare "S01" (a whole-season pack
    // with no episode number — e.g. "Mad.Men.S01.1080p.AMZN.WEB-DL...") — the latter doesn't overlap
    // with EpisodeRegex's matches since "S01E02" has no word boundary between the season digits and "E".
    private static readonly Regex SeasonMarkerRegex = new(@"\b(?:Season\s*\d{1,2}|S\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex YearRegex = new(@"\b(19\d{2}|20\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly string[] ReleaseTags =
    [
        "REPACK", "PROPER", "EXTENDED", "UNRATED", "LIMITED", "REMASTERED", "MULTI",
        "2160p", "1080p", "720p", "480p", "4K",
        "BluRay", "BDRip", "BRRip", "WEB-DL", "WEBRip", "WEB", "HDTV", "DVDRip", "DVDScr", "CAM",
        "x264", "x265", "H264", "H265", "HEVC", "AAC", "AC3", "DTS",
    ];

    public static bool TryParse(string fileName, out string title, out int? year, out MediaKind kind)
    {
        var extension = Path.GetExtension(fileName);
        if (!VideoExtensions.Contains(extension))
        {
            title = "";
            year = null;
            kind = MediaKind.Movie;
            return false;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        return ParseCore(nameWithoutExt, out title, out year, out kind);
    }

    /// <summary>Parses a directory name (e.g. "There Will Be Blood (2007)", "Le.bureau.des.légendes")
    /// the same way as a filename, minus the video-extension gate. A bare folder name with no year or
    /// season/episode marker still falls through to the release-tag-stripping fallback and gets
    /// treated as a plain title (e.g. a show with no disambiguating year in its folder name) — this is
    /// deliberately permissive since the caller only invokes this for a folder the user has explicitly
    /// chosen to look up, not automatically for every directory in a scan.</summary>
    public static bool TryParseDirectoryName(string directoryName, out string title, out int? year, out MediaKind kind) =>
        ParseCore(directoryName, out title, out year, out kind);

    private static bool ParseCore(string rawName, out string title, out int? year, out MediaKind kind)
    {
        title = "";
        year = null;
        kind = MediaKind.Movie;

        // NFC first: macOS (HFS+/APFS) stores accented characters decomposed (NFD, "e" + combining
        // acute) while most other filesystems/TMDb's search use the precomposed form (NFC, "é") — two
        // folders that look identical would otherwise hash to different cache keys and search TMDb
        // with a query string it may not normalize the same way.
        var normalized = rawName.Normalize(NormalizationForm.FormC).Replace('.', ' ').Replace('_', ' ');

        // A name can carry more than one marker at once — e.g. a rebooted show disambiguated by
        // year, "Show.Name.2024.S01.1080p...". A season/episode marker anywhere is the stronger
        // signal (it can't appear in a movie name), so it always decides the kind; the year, if
        // present, is still captured as a search hint either way. The title itself is cut at
        // whichever marker appears earliest in the string.
        var episodeMatch = EpisodeRegex.Match(normalized);
        var seasonMatch = SeasonMarkerRegex.Match(normalized);
        var yearMatch = YearRegex.Match(normalized);
        var isSeries = episodeMatch.Success || seasonMatch.Success;

        var cutIndex = int.MaxValue;
        if (episodeMatch.Success) cutIndex = episodeMatch.Index;
        if (seasonMatch.Success) cutIndex = Math.Min(cutIndex, seasonMatch.Index);
        if (yearMatch.Success) cutIndex = Math.Min(cutIndex, yearMatch.Index);

        if (cutIndex != int.MaxValue)
        {
            kind = isSeries ? MediaKind.Series : MediaKind.Movie;
            year = yearMatch.Success ? int.Parse(yearMatch.Value) : null;
            title = Clean(normalized[..cutIndex]);
            return title.Length > 0;
        }

        kind = MediaKind.Movie;
        title = Clean(StripReleaseTags(normalized));
        return title.Length > 0;
    }

    public static string CacheKey(string title, int? year) =>
        $"{title.Trim().ToLowerInvariant()}|{(year?.ToString() ?? "")}";

    private static string StripReleaseTags(string normalized)
    {
        var earliestIndex = normalized.Length;
        foreach (var tag in ReleaseTags)
        {
            var match = Regex.Match(normalized, $@"\b{Regex.Escape(tag)}\b", RegexOptions.IgnoreCase);
            if (match.Success && match.Index < earliestIndex)
            {
                earliestIndex = match.Index;
            }
        }

        return normalized[..earliestIndex];
    }

    private static string Clean(string candidate)
    {
        var trimmed = candidate.TrimEnd('-', '.', ' ', '(').Trim();
        return WhitespaceRegex.Replace(trimmed, " ");
    }
}
