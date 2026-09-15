using Bot.Models;
using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class SeriesMatcherTests
{
    private static Series MakeSeries(params Episode[] episodes) => new()
    {
        Seasons =
        [
            new Season { SeasonNumber = 1, Episodes = episodes }
        ]
    };

    private static Episode MakeEpisode(int number, params Collection[] collections) => new()
    {
        EpisodeNumber = number,
        Collections = collections
    };

    private static Collection MakeCollection(int id, string? localPath = null) => new()
    {
        Id = id,
        Name = "",
        LocalPath = localPath
    };

    [Fact]
    public void Build_MatchesAnEpisodeWithOneFileAndOneUnmatchedCollection()
    {
        var collection = MakeCollection(1);
        var series = MakeSeries(MakeEpisode(1, collection));
        var files = new[] { new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01.mkv", 1, 1) };

        var plan = SeriesMatcher.Build(series, files);

        var match = Assert.Single(plan.Matches);
        Assert.Equal(1, match.CollectionId);
        Assert.Equal("/data/shows/S01E01.mkv", match.Path);
        Assert.Empty(plan.Skips);
    }

    [Fact]
    public void Build_SkipsWhenNoFileIsFoundOnDisk()
    {
        var series = MakeSeries(MakeEpisode(1, MakeCollection(1)));

        var plan = SeriesMatcher.Build(series, []);

        var skip = Assert.Single(plan.Skips);
        Assert.Equal((1, 1), (skip.Season, skip.Episode));
        Assert.Contains("No file found", skip.Reason);
        Assert.Empty(plan.Matches);
    }

    [Fact]
    public void Build_SkipsWhenSeveralFilesMatchTheSameEpisode()
    {
        var series = MakeSeries(MakeEpisode(1, MakeCollection(1)));
        var files = new[]
        {
            new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01 v1.mkv", 1, 1),
            new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01 v2.mkv", 1, 1)
        };

        var plan = SeriesMatcher.Build(series, files);

        Assert.Empty(plan.Matches);
        Assert.Contains("files on disk", Assert.Single(plan.Skips).Reason);
    }

    [Fact]
    public void Build_SkipsWhenTheEpisodeHasNoCollectionYet()
    {
        var series = MakeSeries(MakeEpisode(1));
        var files = new[] { new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01.mkv", 1, 1) };

        var plan = SeriesMatcher.Build(series, files);

        Assert.Empty(plan.Matches);
        Assert.Contains("No collection tracked", Assert.Single(plan.Skips).Reason);
    }

    [Fact]
    public void Build_SkipsWhenTheEpisodeHasSeveralCollections()
    {
        var series = MakeSeries(MakeEpisode(1, MakeCollection(1), MakeCollection(2)));
        var files = new[] { new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01.mkv", 1, 1) };

        var plan = SeriesMatcher.Build(series, files);

        Assert.Empty(plan.Matches);
        Assert.Contains("collections tracked", Assert.Single(plan.Skips).Reason);
    }

    [Fact]
    public void Build_SkipsAnEpisodeAlreadyMatchedToALocalFile()
    {
        var series = MakeSeries(MakeEpisode(1, MakeCollection(1, localPath: "/data/shows/existing.mkv")));
        var files = new[] { new SeriesMatcher.LocalEpisodeFile("/data/shows/S01E01.mkv", 1, 1) };

        var plan = SeriesMatcher.Build(series, files);

        Assert.Empty(plan.Matches);
        Assert.Contains("Already matched", Assert.Single(plan.Skips).Reason);
    }
}
