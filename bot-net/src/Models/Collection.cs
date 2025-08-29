using System.Text.Json.Serialization;

namespace Bot.Models;

public class Collection
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("movie_id")] public int? MovieId { get; set; }

    [JsonPropertyName("episode_id")] public int? EpisodeId { get; set; }

    [JsonPropertyName("season_id")] public int? SeasonId { get; set; }

    [JsonPropertyName("files")] public File[]? Files { get; set; }

    [JsonPropertyName("quality")] public string? Quality { get; set; }

    [JsonPropertyName("audio_languages")] public string? AudioLanguages { get; set; }

    [JsonPropertyName("subtitle_languages")]
    public string? SubtitleLanguages { get; set; }

    [JsonPropertyName("tags")] public string? Tags { get; set; }

    [JsonPropertyName("notes")] public string? Notes { get; set; }
}