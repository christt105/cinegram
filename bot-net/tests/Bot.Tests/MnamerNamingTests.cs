using Bot.Utils;
using Xunit;

namespace Bot.Tests;

/// <summary>
/// Every expectation here was produced by running mnamer 2.7.2 itself over the title, with
/// the formats mnamer-telegram configures. Regenerate them the same way rather than by
/// hand: a divergence means a download stops landing where mnamer would have put it.
/// </summary>
public class MnamerNamingTests
{
    [Theory]
    [InlineData("Guardianes de la noche: Kimetsu no Yaiba La fortaleza infinita", 2025, 1311031,
        "Guardianes de La Noche Kimetsu No Yaiba La Fortaleza Infinita (2025) [tmdbid-1311031]")]
    [InlineData("Pokémon 2: El poder de uno", 1999, 12599, "Pokémon 2 El Poder de Uno (1999) [tmdbid-12599]")]
    [InlineData("Minions: El origen de Gru", 2022, 438148, "Minions El Origen de Gru (2022) [tmdbid-438148]")]
    [InlineData("Spider-Man: Lejos de casa", 2019, 429617, "Spider-Man Lejos de Casa (2019) [tmdbid-429617]")]
    [InlineData("The Lord of the Rings: The Two Towers", 2002, 121, "The Lord of The Rings The Two Towers (2002) [tmdbid-121]")]
    [InlineData("Fast & Furious", 2009, 13804, "Fast and Furious (2009) [tmdbid-13804]")]
    [InlineData("Hello; goodbye", 2010, 3, "Hello, Goodbye (2010) [tmdbid-3]")]
    [InlineData("Tom @ home", 2001, 1, "Tom at Home (2001) [tmdbid-1]")]
    [InlineData("AC/DC: Live at Donington", 1992, 2, "Ac-DC Live at Donington (1992) [tmdbid-2]")]
    [InlineData("Dr. Strange in the Multiverse of Madness", 2022, 453395, "Dr. Strange in the Multiverse of Madness (2022) [tmdbid-453395]")]
    [InlineData("WALL-E", 2008, 10681, "Wall-E (2008) [tmdbid-10681]")]
    [InlineData("Rocky II", 1979, 1366, "Rocky II (1979) [tmdbid-1366]")]
    [InlineData("Malcolm X", 1992, 614, "Malcolm X (1992) [tmdbid-614]")]
    [InlineData("csi nyc ufo wwii", 2020, 6, "CSI NYC UFO WWII (2020) [tmdbid-6]")]
    [InlineData("Amélie de Montmartre", 2001, 194, "Amélie de Montmartre (2001) [tmdbid-194]")]
    [InlineData("  raro  --  espaciado  ", 2020, 4, "Raro - Espaciado (2020) [tmdbid-4]")]
    public void MovieDirectory_MatchesMnamer(string title, int year, int tmdbId, string expected)
    {
        Assert.Equal(expected, MnamerNaming.MovieDirectory(title, year, tmdbId));
    }

    [Fact]
    public void MovieDirectory_DropsTheEmptyBracketsOfAMissingYear()
    {
        Assert.Equal("Sin Año [tmdbid-999]", MnamerNaming.MovieDirectory("Sin año", null, 999));
    }

    [Fact]
    public void MovieDirectory_DropsTheIdOfAnUnidentifiedTitle()
    {
        Assert.Equal("El Cid (1961)", MnamerNaming.MovieDirectory("El Cid", 1961, null));
    }

    [Fact]
    public void MovieFile_MatchesMnamer()
    {
        Assert.Equal(
            "Guardianes de La Noche Kimetsu No Yaiba La Fortaleza Infinita (2025).mkv",
            MnamerNaming.MovieFile("Guardianes de la noche: Kimetsu no Yaiba La fortaleza infinita", 2025, "", ".mkv"));
    }

    [Fact]
    public void MovieFile_KeepsTheVersionTagJellyfinReads()
    {
        var tag = MediaNaming.BuildVersionTag("1080p", "Erai BDRip");

        Assert.Equal(
            "Guardianes de La Noche Kimetsu No Yaiba La Fortaleza Infinita (2025) - [1080p Erai BDRip].mkv",
            MnamerNaming.MovieFile("Guardianes de la noche: Kimetsu no Yaiba La fortaleza infinita", 2025, tag, ".mkv"));
    }

    [Fact]
    public void MovieFile_AndItsDirectoryAgreeOnTheTitle()
    {
        var directory = MnamerNaming.MovieDirectory("Minions: Cachorro", 2013, 229407);
        var file = MnamerNaming.MovieFile("Minions: Cachorro", 2013, "", ".mkv");

        Assert.StartsWith("Minions Cachorro (2013)", directory);
        Assert.Equal("Minions Cachorro (2013).mkv", file);
    }

    [Fact]
    public void ShowDirectory_PrefersTheTvdbId()
    {
        Assert.Equal("Kimetsu No Yaiba [tvdbid-348545]", MnamerNaming.ShowDirectory("Kimetsu no Yaiba", 348545, 85937));
    }

    [Fact]
    public void ShowDirectory_FallsBackToTheTmdbId()
    {
        Assert.Equal("Kimetsu No Yaiba [tmdbid-85937]", MnamerNaming.ShowDirectory("Kimetsu no Yaiba", null, 85937));
    }

    [Fact]
    public void EpisodeFile_MatchesMnamer()
    {
        Assert.Equal("Kimetsu No Yaiba S01E03.mkv", MnamerNaming.EpisodeFile("Kimetsu no Yaiba", 1, 3, "", ".mkv"));
    }

    [Fact]
    public void SeasonDirectory_PadsTheNumber()
    {
        Assert.Equal("Season 01", MnamerNaming.SeasonDirectory(1));
        Assert.Equal("Season 12", MnamerNaming.SeasonDirectory(12));
    }

    [Theory]
    [InlineData("a: b", "a b")]
    [InlineData("what?", "what")]
    [InlineData("100% real", "100 real")]
    [InlineData("a|b<c>d\"e*f", "abcdef")]
    public void Sanitize_DropsWhatMnamerRefusesToWrite(string value, string expected)
    {
        Assert.Equal(expected, MnamerNaming.Sanitize(value));
    }

    [Fact]
    public void Sanitize_LeavesTheExtensionAlone()
    {
        Assert.Equal("a b.mkv", MnamerNaming.Sanitize("a: b.mkv"));
    }

    [Theory]
    [InlineData("Movie ()", "Movie")]
    [InlineData("Movie []", "Movie")]
    [InlineData("Movie  --  cut", "Movie - cut")]
    [InlineData("-Movie-", "Movie")]
    public void FixPadding_CollapsesWhatAnEmptyFieldLeavesBehind(string value, string expected)
    {
        Assert.Equal(expected, MnamerNaming.FixPadding(value));
    }
}
