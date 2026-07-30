using System.Text.Json.Serialization;

namespace Bot.Models;

public class LocalFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("filesize")] public long Filesize { get; set; }
}
