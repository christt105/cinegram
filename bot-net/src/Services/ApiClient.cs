using System.Net.Http.Json;
using System.Text.Json;

namespace Bot.Services;

/// <summary>The backend's health check. The backend only owns the schema migrations until the
/// single-service release.</summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(string baseUrl = "http://backend:8000")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<Dictionary<string, object>> HealthAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Dictionary<string, object>>("/", _jsonOptions)
                   ?? new Dictionary<string, object> { ["status"] = "unhealthy" };
        }
        catch (HttpRequestException e)
        {
            return new Dictionary<string, object>
            {
                ["status"] = "unhealthy",
                ["error"] = e.Message
            };
        }
    }
}
