using System.Text.Json.Serialization;

namespace Bot.Models;

public class Series
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("manual_title")] public string? ManualTitle { get; set; }

    [JsonPropertyName("tmdb_id")] public int? TmdbId { get; set; }
}
