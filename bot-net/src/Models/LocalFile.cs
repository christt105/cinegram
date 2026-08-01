using System.Text.Json.Serialization;

namespace Bot.Models;

public class LocalFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("filesize")] public long Filesize { get; set; }
    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; set; }
    [JsonPropertyName("version_tag")] public string? VersionTag { get; set; }
    [JsonPropertyName("quality")] public string? Quality { get; set; }
    [JsonPropertyName("tmdb_id")] public int? TmdbId { get; set; }
    [JsonPropertyName("tvdb_id")] public int? TvdbId { get; set; }
}
