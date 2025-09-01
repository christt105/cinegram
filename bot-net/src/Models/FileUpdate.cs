using System.Text.Json.Serialization;

namespace Bot.Models;

public class FileUpdate
{
    [JsonPropertyName("collection_id")]
    public int CollectionId { get; set; }
}
