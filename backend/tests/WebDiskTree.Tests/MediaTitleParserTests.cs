using WebDiskTree.Core.Models;

namespace WebDiskTree.Tests;

public class MediaTitleParserTests
{
    [Fact]
    public void ParsesMovieWithYearAndReleaseTags()
    {
        var ok = MediaTitleParser.TryParse(
            "There.Will.Be.Blood.2007.REPACK.720p.BluRay.x264-DON.mkv",
            out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("There Will Be Blood", title);
        Assert.Equal(2007, year);
        Assert.Equal(MediaKind.Movie, kind);
    }

    [Fact]
    public void ParsesSeriesWithSeasonEpisodeMarker()
    {
        var ok = MediaTitleParser.TryParse(
            "Breaking.Bad.S01E02.720p.WEB-DL.x264-GROUP.mkv",
            out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Breaking Bad", title);
        Assert.Null(year);
        Assert.Equal(MediaKind.Series, kind);
    }

    [Fact]
    public void ParsesMovieWithNoYearByStrippingReleaseTags()
    {
        var ok = MediaTitleParser.TryParse(
            "Some.Movie.Name.LIMITED.BluRay.x264-GROUP.mkv",
            out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Some Movie Name", title);
        Assert.Null(year);
        Assert.Equal(MediaKind.Movie, kind);
    }

    [Fact]
    public void RejectsNonVideoExtensions()
    {
        var ok = MediaTitleParser.TryParse("vacation-photo.jpg", out _, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void ParsesDirectoryNameWithYearInParentheses()
    {
        var ok = MediaTitleParser.TryParseDirectoryName(
            "There Will Be Blood (2007)", out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("There Will Be Blood", title);
        Assert.Equal(2007, year);
        Assert.Equal(MediaKind.Movie, kind);
    }

    [Fact]
    public void ParsesDirectoryNameWithSeasonMarker()
    {
        var ok = MediaTitleParser.TryParseDirectoryName(
            "Breaking.Bad.Season.1", out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Breaking Bad", title);
        Assert.Null(year);
        Assert.Equal(MediaKind.Series, kind);
    }

    [Fact]
    public void ParsesDirectoryNameWithBareSeasonPack()
    {
        // Sonarr/Radarr-style whole-season release naming — no "Season" word, no episode number.
        var ok = MediaTitleParser.TryParseDirectoryName(
            "Mad.Men.S01.1080p.AMZN.WEB-DL.DD+5.1.H.264-playWEB", out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Mad Men", title);
        Assert.Null(year);
        Assert.Equal(MediaKind.Series, kind);
    }

    [Fact]
    public void SeasonMarkerWinsKindButYearIsStillCapturedAsASearchHint()
    {
        // A rebooted show disambiguated by year — the season marker proves it's a series (a movie
        // name can't contain one), but the year is still useful to narrow the TMDb search.
        var ok = MediaTitleParser.TryParseDirectoryName(
            "Show.Name.2024.S01.1080p", out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Show Name", title);
        Assert.Equal(2024, year);
        Assert.Equal(MediaKind.Series, kind);
    }

    [Fact]
    public void ParsesPlainDirectoryNameWithNoMarkerAsATitle()
    {
        // No year, no season/episode marker, no release tags — a show/movie folder named with
        // nothing but its title (e.g. "Le.bureau.des.légendes"). Looking this up is an explicit,
        // user-triggered action on one chosen folder, not something run automatically over every
        // directory in a scan, so guessing here is an acceptable trade-off for catching this case.
        var ok = MediaTitleParser.TryParseDirectoryName("Le.bureau.des.légendes", out var title, out var year, out var kind);

        Assert.True(ok);
        Assert.Equal("Le bureau des légendes", title);
        Assert.Null(year);
        Assert.Equal(MediaKind.Movie, kind);
    }

    [Fact]
    public void NfcAndNfdUnicodeVariantsOfTheSameTitleProduceTheSameCacheKey()
    {
        // macOS filesystems store accented names decomposed (NFD); a folder synced from elsewhere
        // may keep the precomposed form (NFC) instead. Both spell "légendes" but are different byte
        // sequences, so without normalizing they'd hash to two different cache keys.
        const string nfc = "Le.bureau.des.légendes";
        const string nfd = "Le.bureau.des.légendes";

        MediaTitleParser.TryParseDirectoryName(nfc, out var nfcTitle, out var nfcYear, out _);
        MediaTitleParser.TryParseDirectoryName(nfd, out var nfdTitle, out var nfdYear, out _);

        Assert.Equal(nfcTitle, nfdTitle);
        Assert.Equal(MediaTitleParser.CacheKey(nfcTitle, nfcYear), MediaTitleParser.CacheKey(nfdTitle, nfdYear));
    }

    [Fact]
    public void CacheKeyIsCaseInsensitiveAndIncludesYear()
    {
        var key1 = MediaTitleParser.CacheKey("There Will Be Blood", 2007);
        var key2 = MediaTitleParser.CacheKey("there will be blood", 2007);
        var key3 = MediaTitleParser.CacheKey("There Will Be Blood", null);

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
    }
}
