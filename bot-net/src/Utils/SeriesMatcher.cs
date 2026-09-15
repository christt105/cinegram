using System.Text.Json.Serialization;
using Bot.Models;

namespace Bot.Utils;

/// <summary>
/// Decides, for a series' season/episode tree and the local files parsed off disk, which
/// episodes can be safely linked to a file without a human picking. Pure: no ffprobe, no
/// HTTP, so the decision can be tested without touching the library or the backend.
/// </summary>
public static class SeriesMatcher
{
    public record LocalEpisodeFile(string Path, int Season, int Episode);

    public record Match(
        [property: JsonPropertyName("collection_id")] int CollectionId,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("season")] int Season,
        [property: JsonPropertyName("episode")] int Episode);

    public record Skip(
        [property: JsonPropertyName("season")] int Season,
        [property: JsonPropertyName("episode")] int Episode,
        [property: JsonPropertyName("reason")] string Reason);

    public record Plan(
        [property: JsonPropertyName("matches")] List<Match> Matches,
        [property: JsonPropertyName("skips")] List<Skip> Skips);

    /// <summary>
    /// Matches each episode to a local file when the link is unambiguous: exactly one file
    /// on disk for that season/episode, exactly one collection tracking the episode, and
    /// that collection has no local file yet. Everything else is reported as a skip with
    /// the reason, left for the existing manual probe modal to resolve.
    /// </summary>
    public static Plan Build(Series series, IEnumerable<LocalEpisodeFile> localFiles)
    {
        var filesByEpisode = localFiles
            .GroupBy(f => (f.Season, f.Episode))
            .ToDictionary(g => g.Key, g => g.ToList());

        var matches = new List<Match>();
        var skips = new List<Skip>();

        foreach (var season in series.Seasons ?? [])
        {
            foreach (var episode in season.Episodes ?? [])
            {
                filesByEpisode.TryGetValue((season.SeasonNumber, episode.EpisodeNumber), out var files);
                var collections = episode.Collections ?? [];

                if (files is null || files.Count == 0)
                {
                    skips.Add(new Skip(season.SeasonNumber, episode.EpisodeNumber,
                        "No file found on disk for this episode."));
                    continue;
                }

                if (files.Count > 1)
                {
                    skips.Add(new Skip(season.SeasonNumber, episode.EpisodeNumber,
                        $"{files.Count} files on disk match this episode; pick one manually."));
                    continue;
                }

                if (collections.Length == 0)
                {
                    skips.Add(new Skip(season.SeasonNumber, episode.EpisodeNumber,
                        "No collection tracked for this episode yet."));
                    continue;
                }

                if (collections.Length > 1)
                {
                    skips.Add(new Skip(season.SeasonNumber, episode.EpisodeNumber,
                        $"{collections.Length} collections tracked for this episode; pick one manually."));
                    continue;
                }

                var collection = collections[0];
                if (!string.IsNullOrWhiteSpace(collection.LocalPath))
                {
                    skips.Add(new Skip(season.SeasonNumber, episode.EpisodeNumber,
                        "Already matched to a local file."));
                    continue;
                }

                matches.Add(new Match(collection.Id, files[0].Path, season.SeasonNumber, episode.EpisodeNumber));
            }
        }

        return new Plan(matches, skips);
    }
}
