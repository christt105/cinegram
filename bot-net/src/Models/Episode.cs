using System.Text.Json.Serialization;

namespace Bot.Models;

public class Episode
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("episode_number")] public int EpisodeNumber { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("collections")] public Collection[]? Collections { get; set; }
}
