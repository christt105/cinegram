using Bot.Services;
using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class TmdbIdentificationTests
{
    [Theory]
    [InlineData("Pokémon", "pokemon")]
    [InlineData("Pokemon", "pokemon")]
    [InlineData("WALL·E", "wall e")]
    [InlineData("Wall-E", "wall e")]
    [InlineData("Wall E", "wall e")]
    [InlineData("Spider-Man", "spider man")]
    [InlineData("Amélie", "amelie")]
    [InlineData("Jurassic World – Dominion", "jurassic world dominion")]
    [InlineData("", "")]
    public void Normalize_StripsDiacriticsAndSeparators(string raw, string expected) =>
        Assert.Equal(expected, TitleMatcher.Normalize(raw));

    [Theory]
    [InlineData("parasite", "parasitos", 0.8235294117647058)]
    [InlineData("the punisher", "marvel the punisher", 0.7741935483870968)]
    [InlineData("kung fu panda 3", "kung fu panda 2", 0.9333333333333333)]
    [InlineData("abcd", "bcda", 0.75)]
    [InlineData("", "abc", 0.0)]
    [InlineData("", "", 1.0)]
    public void Similarity_MatchesDifflibRatio(string a, string b, double expected) =>
        Assert.Equal(expected, TitleMatcher.Similarity(a, b), 12);

    [Fact]
    public void Similarity_JunksPopularCharactersInLongSequencesLikeDifflib() =>
        Assert.Equal(0.0, TitleMatcher.Similarity(new string('a', 150) + new string('b', 100),
            new string('b', 120) + new string('a', 130)));

    [Theory]
    [InlineData("Pokemon", "Pokémon", "Digimon", "Pokémon")]
    [InlineData("Pokémon", "Pokemon", "Monster Rancher", "Pokemon")]
    [InlineData("Wall-E", "WALL·E", "Cars", "WALL·E")]
    [InlineData("Wall E", "WALL·E", "Up", "WALL·E")]
    [InlineData("Spider-Man", "Spider-Man", "Superman", "Spider-Man")]
    [InlineData("Amelie", "Amélie", "Delicatessen", "Amélie")]
    public void BestMatch_NormalizesDiacritics(string query, string first, string second, string expected)
    {
        var results = new[] { Tv(1, first, popularity: 1), Tv(2, second, popularity: 1) };

        Assert.Equal(expected, TitleMatcher.BestMatch(results, query)?.Name);
    }

    [Fact]
    public void BestMatch_RejectsBelowThreshold() =>
        Assert.Null(TitleMatcher.BestMatch([Tv(1, "Completely Unrelated Show", popularity: 50)], "Pokémon"));

    [Fact]
    public void BestMatch_EmptyResults() => Assert.Null(TitleMatcher.BestMatch([], "Pokémon"));

    [Fact]
    public void BestMatch_FallsBackToTheOriginalNameForLocalizedResults()
    {
        var results = new[]
        {
            Tv(1, "Bajo escucha", "The Wire", 58.0),
            Tv(2, "The LiveWire", "The LiveWire", 0.8),
        };

        var match = TitleMatcher.BestMatch(results, "The Wire");

        Assert.Equal("The Wire", match?.OriginalName);
        Assert.Equal(1.0, match?.MatchScore);
    }

    [Fact]
    public void BestMatch_PrefersThePopularResultAmongNearTies()
    {
        var results = new[]
        {
            Movie(1, "Parásitos", "기생충", 39.9387),
            Movie(2, "Parasites", "Parasites", 0.9424),
        };

        Assert.Equal("Parásitos", TitleMatcher.BestMatch(results, "Parasite")?.Title);
    }

    [Fact]
    public void BestMatch_PrefersThePopularResultForAMarvelPrefixedTitle()
    {
        var results = new[]
        {
            Tv(1, "Marvel - The Punisher", "Marvel's The Punisher", 61.5041),
            Tv(2, "The Punisher", "The Punisher", 0.6121),
        };

        Assert.Equal("Marvel - The Punisher", TitleMatcher.BestMatch(results, "The Punisher")?.Name);
    }

    [Fact]
    public void BestMatch_DoesNotLetPopularityOverrideAClearSequelMatch()
    {
        var results = new[]
        {
            Movie(1, "Kung Fu Panda 3", "Kung Fu Panda 3", 16.6993),
            Movie(2, "Kung Fu Panda 2", "Kung Fu Panda 2", 18.1816),
        };

        Assert.Equal("Kung Fu Panda 3", TitleMatcher.BestMatch(results, "Kung Fu Panda 3")?.Title);
    }

    [Fact]
    public async Task SearchAndMatch_PassesTheYearToAMovieSearch()
    {
        var api = new FakeTmdbApi { Results = { ["movie"] = [Movie(603, "Matrix", popularity: 47.5)] } };

        var result = await new TmdbIdentifier(api).SearchAndMatchAsync("The Matrix", "movie", 1999);

        Assert.Equal(("movie", "The Matrix", 1999), api.Searches[0]);
        Assert.Equal(603, result?.Id);
    }

    [Fact]
    public async Task SearchAndMatch_PassesTheYearToATvSearch()
    {
        var api = new FakeTmdbApi { Results = { ["tv"] = [Tv(1438, "Bajo escucha", "The Wire", 58.0)] } };

        var result = await new TmdbIdentifier(api).SearchAndMatchAsync("The Wire", "tv", 2002);

        Assert.Equal(("tv", "The Wire", 2002), api.Searches[0]);
        Assert.Equal(1438, result?.Id);
    }

    [Fact]
    public async Task SearchAndMatch_FallsBackToAMultiSearchWithoutTheYear()
    {
        var api = new FakeTmdbApi();

        await new TmdbIdentifier(api).SearchAndMatchAsync("Some Movie", "movie", null);

        Assert.Equal([("movie", "Some Movie", null), ("multi", "Some Movie", null)], api.Searches);
    }

    [Fact]
    public async Task SearchAndMatch_KeepsTheMediaTypeOfTheMultiResultItPicks()
    {
        var api = new FakeTmdbApi
        {
            Results = { ["multi"] = [Movie(1, "Unrelated Film"), Tv(2, "Some Show")] },
        };

        var result = await new TmdbIdentifier(api).SearchAndMatchAsync("Some Show", "movie", null);

        Assert.Equal((2, "tv"), (result?.Id, result?.MediaType));
    }

    [Fact]
    public async Task IdentifyByFilename_WithAnEmbeddedTmdbIdSkipsTheTitleSearch()
    {
        var api = new FakeTmdbApi { Details = { [("movie", 149870)] = Movie(149870, "El viento se levanta") } };

        var result = await new TmdbIdentifier(api).IdentifyByFilenameAsync(
            "El viento se levanta (2013) [tmdbid-149870].mkv");

        Assert.Equal(149870, result?.Id);
        Assert.Empty(api.Searches);
    }

    [Fact]
    public async Task IdentifyByFilename_LooksUpAnEmbeddedTvIdAsASeriesFirst()
    {
        var api = new FakeTmdbApi { Details = { [("tv", 31910)] = Tv(31910, "Naruto Shippuden") } };

        var result = await new TmdbIdentifier(api).IdentifyByFilenameAsync(
            "Naruto Shippuden - S07E02 - [tmdbid-31910].avi");

        Assert.Equal([("tv", 31910)], api.Lookups);
        Assert.Equal(31910, result?.Id);
        Assert.Empty(api.Searches);
    }

    [Fact]
    public async Task IdentifyByFilename_RetriesWithTheNormalizedTitleForAccentedNames()
    {
        var api = new FakeTmdbApi();
        api.OnSearch = (kind, query) => query == "pequena miss sunshine" && kind == "movie"
            ? [Movie(773, "Pequena Miss Sunshine")]
            : [];

        var result = await new TmdbIdentifier(api).IdentifyByFilenameAsync("Pequeña Miss Sunshine (2006).mkv");

        Assert.Equal(["Pequeña Miss Sunshine", "Pequeña Miss Sunshine", "pequena miss sunshine"],
            api.Searches.Select(s => s.Query));
        Assert.Equal(773, result?.Id);
    }

    [Fact]
    public async Task IdentifyByFilename_DoesNotRetryWhenNormalizationIsANoOp()
    {
        var api = new FakeTmdbApi();

        var result = await new TmdbIdentifier(api).IdentifyByFilenameAsync("Show S01E02.mkv");

        Assert.All(api.Searches, s => Assert.Equal("Show", s.Query));
        Assert.Null(result);
    }

    [Fact]
    public async Task IdentifyByTmdbId_FallsBackToTheOtherMediaType()
    {
        var api = new FakeTmdbApi { Details = { [("tv", 42)] = Tv(42, "Stub Show") } };

        var result = await new TmdbIdentifier(api).IdentifyByTmdbIdAsync(42, "movie");

        Assert.Equal([("movie", 42), ("tv", 42)], api.Lookups);
        Assert.Equal("tv", result?.MediaType);
    }

    [Fact]
    public void Parse_ReadsATmdbSearchResult()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            """{"id": 1438, "name": "Bajo escucha", "original_name": "The Wire", "first_air_date": "2002-06-02", "popularity": 58.1, "media_type": "tv", "title": ""}""");

        var title = TmdbApi.Parse(doc.RootElement, null);

        Assert.Equal(new TmdbTitle(1438, "tv", null, "Bajo escucha", null, "The Wire", null, "2002-06-02", 58.1), title);
        Assert.Equal(("Bajo escucha", 2002), (title!.DisplayTitle, title.Year));
    }

    [Fact]
    public void Parse_SkipsPeopleInAMultiSearch()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{"id": 5, "name": "Someone", "media_type": "person"}""");

        Assert.Null(TmdbApi.Parse(doc.RootElement, null));
    }

    internal static TmdbTitle Movie(int id, string title, string? original = null, double popularity = 0, string? releaseDate = null) =>
        new(id, "movie", Title: title, OriginalTitle: original, ReleaseDate: releaseDate, Popularity: popularity);

    internal static TmdbTitle Tv(int id, string name, string? original = null, double popularity = 0, string? firstAirDate = null) =>
        new(id, "tv", Name: name, OriginalName: original, FirstAirDate: firstAirDate, Popularity: popularity);

    private class FakeTmdbApi : ITmdbApi
    {
        public Dictionary<(string, int), TmdbTitle> Details { get; } = new();
        public Dictionary<string, List<TmdbTitle>> Results { get; } = new();
        public Func<string, string, List<TmdbTitle>>? OnSearch { get; set; }
        public List<(string Kind, string Query, int? Year)> Searches { get; } = [];
        public List<(string, int)> Lookups { get; } = [];

        public Task<TmdbTitle?> GetAsync(string mediaType, int id, CancellationToken ct = default)
        {
            Lookups.Add((mediaType, id));
            return Task.FromResult(Details.GetValueOrDefault((mediaType, id)));
        }

        public Task<IReadOnlyList<TmdbTitle>> SearchAsync(string kind, string query, int? year, CancellationToken ct = default)
        {
            Searches.Add((kind, query, year));
            var results = OnSearch?.Invoke(kind, query) ?? Results.GetValueOrDefault(kind) ?? [];
            return Task.FromResult<IReadOnlyList<TmdbTitle>>(results);
        }

        public Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default) => Task.FromResult<int?>(null);
    }
}
