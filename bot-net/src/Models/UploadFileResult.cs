using System.Text.Json.Serialization;

namespace Bot.Models;

public class UploadFileResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("message_id")]
    public int? MessageId { get; set; }
    
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    
    [JsonPropertyName("collection_id")]
    public int? CollectionId { get; set; }
}