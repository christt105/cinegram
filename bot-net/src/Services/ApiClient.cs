using Bot.Models;

namespace Bot.Services;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

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

    public async Task<UploadFileResult> UploadAsync(UploadFile fileMeta)
    {
        var payload = new
        {
            message_id = fileMeta.MessageId,
            filename = fileMeta.FileName,
            filesize = fileMeta.FileSize,
            mime_type = fileMeta.MimeType,
            created_at = fileMeta.UploadDate ?? DateTime.UtcNow.ToString("o") // ISO 8601
        };

        var response = await _httpClient.PostAsJsonAsync("/upload", payload);
        
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new Exception($"Upload failed: {response.StatusCode} {text}");
        }
    
        var result = await response.Content.ReadFromJsonAsync<UploadFileResult>(_jsonOptions);
        if (result == null)
            throw new Exception("Upload failed: response is null");

        return result;
    }

    public Task<List<Dictionary<string, object>>?> GetMoviesAsync()
        => _httpClient.GetFromJsonAsync<List<Dictionary<string, object>>>("/movies", _jsonOptions);

    public Task<Dictionary<string, object>?> GetMovieAsync(int localId)
        => _httpClient.GetFromJsonAsync<Dictionary<string, object>>($"/movies/{localId}", _jsonOptions);

    public Task<List<Dictionary<string, object>>?> SearchMoviesAsync(string query)
        => _httpClient.GetFromJsonAsync<List<Dictionary<string, object>>>($"/movies/search?q={Uri.EscapeDataString(query)}", _jsonOptions);

    public async Task<Dictionary<string, object>?> GetMovieByTmdbAsync(int tmdbId)
    {
        var resp = await _httpClient.GetAsync($"/movies/tmdb/{tmdbId}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
    }

    public async Task<List<Dictionary<string, object>>> GetCollectionsAsync(int movieId)
    {
        var resp = await _httpClient.GetAsync($"/movies/{movieId}/collections");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new List<Dictionary<string, object>>();

        return await resp.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(_jsonOptions)
               ?? new List<Dictionary<string, object>>();
    }

    public async Task<Dictionary<string, object>?> GetCollectionAsync(int collectionId)
    {
        var resp = await _httpClient.GetAsync($"/collections/{collectionId}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
    }

    public async Task<Dictionary<string, object>?> GetFileAsync(int fileId)
    {
        var resp = await _httpClient.GetAsync($"/files/{fileId}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
    }

    public async Task<(Dictionary<string, object>?, int)> PatchFileAsync(int fileId, object data)
    {
        var resp = await _httpClient.PatchAsync($"/files/{fileId}", JsonContent.Create(data));
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (null, 404);

        var content = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
        return (content, (int)resp.StatusCode);
    }

    public async Task<bool> DeleteFileAsync(int fileId)
    {
        var resp = await _httpClient.DeleteAsync($"/files/{fileId}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteCollectionAsync(int collectionId)
    {
        var resp = await _httpClient.DeleteAsync($"/collections/{collectionId}");
        return resp.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
