using System.Text.Json.Serialization;

namespace Bot.Models;

public class UploadTask
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("jellyfin_id")] public string JellyfinId { get; set; }
    [JsonPropertyName("tmdb_id")] public int? TmdbId { get; set; }
    [JsonPropertyName("media_type")] public string MediaType { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; }
    [JsonPropertyName("progress")] public int Progress { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
}
