using System.Text.Json.Serialization;

namespace Bot.Models;

public class QueueDownloadTask
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("collection_id")] public int CollectionId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; }
    [JsonPropertyName("progress")] public int Progress { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
}
