using System.Text.Json.Serialization;

namespace Bot.Models;

public class Series
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("manual_title")] public string? ManualTitle { get; set; }

    [JsonPropertyName("tmdb_id")] public int? TmdbId { get; set; }

    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }

    [JsonPropertyName("overview")] public string? Overview { get; set; }

    [JsonPropertyName("release_year")] public int? ReleaseYear { get; set; }

    [JsonPropertyName("seasons")] public Season[]? Seasons { get; set; }
}
