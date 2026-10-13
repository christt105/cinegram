using System.Text.Json.Serialization;
using Bot.Data;

namespace Bot.Models;

/// <summary>The settled identity of a watched file: what a move needs to build the destination
/// path, and what /watch/{id}/confirm and /watch/{id}/correct return to the web.</summary>
public class WatchedFileResolution
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("tmdb_id")] public int TmdbId { get; set; }
    [JsonPropertyName("tvdb_id")] public int? TvdbId { get; set; }
    [JsonPropertyName("media_type")] public string MediaType { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("season")] public int? Season { get; set; }
    [JsonPropertyName("episode")] public int? Episode { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";

    /// <summary>Null when the row has no settled TMDB identity yet.</summary>
    public static WatchedFileResolution? FromWatchedFile(WatchedFile row)
    {
        if (row.GuessTmdbId is null || row.GuessMediaType is null || row.GuessTitle is null) return null;

        return new WatchedFileResolution
        {
            Id = row.Id,
            Path = row.Path,
            Filename = row.Filename,
            TmdbId = row.GuessTmdbId.Value,
            TvdbId = row.GuessTvdbId,
            MediaType = row.GuessMediaType,
            Title = row.GuessTitle,
            Year = row.GuessYear,
            Season = row.GuessSeason,
            Episode = row.GuessEpisode,
            Status = row.Status,
        };
    }
}
