using System.Text.Json.Serialization;

namespace Bot.Models;

public class File
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("message_id")] public int MessageId { get; set; }

    [JsonPropertyName("filename")] public string FileName { get; set; }

    [JsonPropertyName("filesize")] public long FileSize { get; set; }

    [JsonPropertyName("mime_type")] public string MimeType { get; set; }

    [JsonPropertyName("created_at")] public string CreatedAt { get; set; }

    [JsonPropertyName("collection_id")] public int? CollectionId { get; set; }
}