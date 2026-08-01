using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class MediaNameParserTests
{
    private const string MoviePath =
        "/data/movies/La fortaleza infinita (2025) [tmdbid-1311031]/La fortaleza infinita (2025) - [1080p Erai BDRip].mkv";

    private const string EpisodePath =
        "/data/shows/Kimetsu no Yaiba [tvdbid-348545]/Season 01/Kimetsu no Yaiba - S01E01 - [720p].mkv";

    [Fact]
    public void ParseTmdbId_ReadsTheIdFromTheFolder()
    {
        Assert.Equal(1311031, MediaNameParser.ParseTmdbId(MoviePath));
    }

    [Fact]
    public void ParseTmdbId_ReturnsNullWhenTheFolderCarriesNoId()
    {
        Assert.Null(MediaNameParser.ParseTmdbId("/data/movies/La fortaleza infinita (2025)/movie.mkv"));
    }

    [Fact]
    public void ParseTmdbId_KeepsTheDeepestIdOfTheChain()
    {
        var path = "/data/movies/Saga [tmdbid-1]/Part (2025) [tmdbid-2]/Part.mkv";

        Assert.Equal(2, MediaNameParser.ParseTmdbId(path));
    }

    [Fact]
    public void ParseTvdbId_ReadsTheIdFromTheShowFolder()
    {
        Assert.Equal(348545, MediaNameParser.ParseTvdbId(EpisodePath));
        Assert.Null(MediaNameParser.ParseTmdbId(EpisodePath));
    }

    [Fact]
    public void MatchesIds_WithoutIdsLetsEverythingThrough()
    {
        Assert.True(MediaNameParser.MatchesIds(MoviePath, null, null));
    }

    [Fact]
    public void MatchesIds_KeepsOnlyTheRequestedTitle()
    {
        Assert.True(MediaNameParser.MatchesIds(MoviePath, 1311031, null));
        Assert.False(MediaNameParser.MatchesIds(MoviePath, 999, null));
    }

    [Fact]
    public void MatchesIds_AcceptsAShowFiledUnderEitherId()
    {
        Assert.True(MediaNameParser.MatchesIds(EpisodePath, 42, 348545));
        Assert.False(MediaNameParser.MatchesIds(EpisodePath, 42, 999));
    }

    [Fact]
    public void ParseVersionTag_ReadsTheTagWrittenOnDownload()
    {
        Assert.Equal("1080p Erai BDRip", MediaNameParser.ParseVersionTag(MoviePath));
    }

    [Fact]
    public void ParseVersionTag_RoundTripsWhatMediaNamingBuilds()
    {
        var tag = MediaNaming.BuildVersionTag("2160p", "Remux");
        var filename = $"/data/movies/Movie (2025) [tmdbid-7]/Movie (2025){tag}.mkv";

        Assert.Equal("2160p Remux", MediaNameParser.ParseVersionTag(filename));
    }

    [Fact]
    public void ParseVersionTag_IgnoresIdBrackets()
    {
        Assert.Null(MediaNameParser.ParseVersionTag("/data/movies/Movie [tmdbid-7]/Movie [tmdbid-7].mkv"));
    }

    [Fact]
    public void ParseVersionTag_ReturnsNullWhenThereIsNoTag()
    {
        Assert.Null(MediaNameParser.ParseVersionTag("/data/movies/Movie (2025)/Movie (2025).mkv"));
    }

    [Theory]
    [InlineData("/data/movies/Movie/Movie (2025) - [1080p Erai].mkv", "1080p")]
    [InlineData("/data/movies/Movie/Movie.2025.2160p.WEB-DL.x265.mkv", "2160p")]
    [InlineData("/data/movies/Movie/Movie 4K remux.mkv", "4K")]
    [InlineData("/data/movies/Movie/Movie (2025).mkv", null)]
    [InlineData("/data/movies/Movie/Movie x265 h264.mkv", null)]
    public void ParseQuality_ReadsTheResolutionFromTheFilename(string path, string? expected)
    {
        Assert.Equal(expected, MediaNameParser.ParseQuality(path));
    }

    [Fact]
    public void ParseQuality_LooksAtTheFilenameOnly()
    {
        Assert.Null(MediaNameParser.ParseQuality("/data/movies/1080p rips/Movie (2025).mkv"));
    }
}
