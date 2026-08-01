using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class PathTranslatorTests
{
    private static readonly PathMapping[] Mappings =
    [
        new("/media/disco/Peliculas", "/data/import/movies"),
        new("/media/disco/Series", "/data/import/shows")
    ];

    [Fact]
    public void Translate_RewritesTheMatchingPrefix()
    {
        Assert.Equal(
            "/data/import/movies/Enola Holmes 3 (2026)/Enola Holmes 3 (2026).mkv",
            PathTranslator.Translate("/media/disco/Peliculas/Enola Holmes 3 (2026)/Enola Holmes 3 (2026).mkv", Mappings));
    }

    [Fact]
    public void Translate_PicksTheMappingThatMatches()
    {
        Assert.Equal(
            "/data/import/shows/Pokémon/Season 01",
            PathTranslator.Translate("/media/disco/Series/Pokémon/Season 01", Mappings));
    }

    [Fact]
    public void Translate_MapsThePrefixItself()
    {
        Assert.Equal("/data/import/movies", PathTranslator.Translate("/media/disco/Peliculas", Mappings));
    }

    [Fact]
    public void Translate_OnlyRewritesTheLeadingPrefix()
    {
        Assert.Equal(
            "/data/import/movies/backup/media/disco/Peliculas/Movie.mkv",
            PathTranslator.Translate("/media/disco/Peliculas/backup/media/disco/Peliculas/Movie.mkv", Mappings));
    }

    [Fact]
    public void Translate_LeavesUnmatchedPathsAlone()
    {
        Assert.Equal("/elsewhere/Movie.mkv", PathTranslator.Translate("/elsewhere/Movie.mkv", Mappings));
    }

    [Fact]
    public void ParseMap_ReadsCommaSeparatedPairs()
    {
        var parsed = PathTranslator.ParseMap(
            "/media/disco/Peliculas:/data/import/movies, /media/disco/Series:/data/import/shows").ToArray();

        Assert.Equal(Mappings, parsed);
    }

    [Fact]
    public void ParseMap_TrimsTrailingSlashes()
    {
        var parsed = PathTranslator.ParseMap("/media/movies/:/data/import/movies/").Single();

        Assert.Equal(new PathMapping("/media/movies", "/data/import/movies"), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/media/movies")]
    [InlineData("/media/movies:")]
    [InlineData(":/data/import/movies")]
    public void ParseMap_SkipsWhatItCannotRead(string? map)
    {
        Assert.Empty(PathTranslator.ParseMap(map));
    }
}
