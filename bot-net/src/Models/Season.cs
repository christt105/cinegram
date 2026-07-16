using System.Text.Json.Serialization;

namespace Bot.Models;

public class Season
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("season_number")] public int SeasonNumber { get; set; }
    [JsonPropertyName("episodes")] public Episode[]? Episodes { get; set; }
    [JsonPropertyName("collections")] public Collection[]? Collections { get; set; }
}
