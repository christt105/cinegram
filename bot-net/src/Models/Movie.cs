using System.Text.Json.Serialization;

namespace Bot.Models;

public class Movie
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("tmdb_id")] public int? TmdbId { get; set; }

    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }

    [JsonPropertyName("collections")] public Collection[]? Collections { get; set; }

    [JsonPropertyName("release_year")] public int? ReleaseYear { get; set; }

    [JsonPropertyName("overview")] public string? Overview { get; set; }

    [JsonPropertyName("tags")] public string? Tags { get; set; }

    [JsonPropertyName("notes")] public string? Notes { get; set; }
}