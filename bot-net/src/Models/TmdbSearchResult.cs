using System.Text.Json.Serialization;

namespace Bot.Models;

public class TmdbSearchResult
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("media_type")] public string MediaType { get; set; }
    [JsonPropertyName("year")] public string Year { get; set; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
}
