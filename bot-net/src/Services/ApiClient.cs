using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bot.Models;

namespace Bot.Services;

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

    public async Task<List<Movie>?> GetMoviesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<Movie>>("/movies", _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    public async Task<Movie?> GetMovieAsync(int localId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Movie>($"/movies/{localId}", _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Movie?> GetMovieByTmdbAsync(int tmdbId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Movie>($"/movies/tmdb/{tmdbId}", _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<List<Dictionary<string, object>>?> SearchMoviesAsync(string query)
    {
        return _httpClient.GetFromJsonAsync<List<Dictionary<string, object>>>(
            $"/movies/search?q={Uri.EscapeDataString(query)}", _jsonOptions);
    }

    public async Task<List<Dictionary<string, object>>> GetCollectionsAsync(int movieId)
    {
        var resp = await _httpClient.GetAsync($"/movies/{movieId}/collections");
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new List<Dictionary<string, object>>();

        return await resp.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(_jsonOptions)
               ?? new List<Dictionary<string, object>>();
    }

    public async Task<Dictionary<string, object>?> GetCollectionAsync(int collectionId)
    {
        var resp = await _httpClient.GetAsync($"/collections/{collectionId}");
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        return await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
    }

    public async Task<Dictionary<string, object>?> GetFileAsync(int fileId)
    {
        var resp = await _httpClient.GetAsync($"/files/{fileId}");
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        return await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
    }

    public async Task<(Dictionary<string, object>?, int)> PatchFileAsync(int fileId, object data)
    {
        var resp = await _httpClient.PatchAsync($"/files/{fileId}", JsonContent.Create(data));
        if (resp.StatusCode == HttpStatusCode.NotFound)
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
}