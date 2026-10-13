using System.Net.Http.Json;
using System.Text.Json;

namespace Bot.Services;

/// <summary>The backend's health check and database backup, until maintenance moves to bot-net.</summary>
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

    public async Task<byte[]?> DownloadBackupAsync()
    {
        var response = await _httpClient.GetAsync("/maintenance/backup");
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            Log.Error($"Backup failed: {response.StatusCode} {text}");
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync();
    }
}