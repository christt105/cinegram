using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bot.Models;
using File = Bot.Models.File;

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
            created_at = fileMeta.UploadDate ?? DateTime.UtcNow.ToString("o"), // ISO 8601
            tmdb_id = fileMeta.TmdbId
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

    public async Task<bool> IdentifyCollectionAsync(int collectionId, int tmdbId)
    {
        var payload = new { tmdb_id = tmdbId };
        var response = await _httpClient.PostAsJsonAsync($"/collections/{collectionId}/identify", payload);
        
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            Log.Error($"Identify failed: {response.StatusCode} {text}");
            return false;
        }
        
        return true;
    }

    private async Task<T?> GetSafeAsync<T>(string url)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<T>(url, _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    public async Task<List<Movie>?> GetMoviesAsync()
    {
        return await GetSafeAsync<List<Movie>>("/movies");
    }

    public async Task<List<Series>?> GetSeriesAsync()
    {
        return await GetSafeAsync<List<Series>>("/series");
    }

    public async Task<Movie?> GetMovieAsync(int localId)
    {
        return await GetSafeAsync<Movie>($"/movies/{localId}");
    }

    public async Task<Movie?> GetMovieByTmdbAsync(int tmdbId)
    {
        return await GetSafeAsync<Movie>($"/movies/tmdb/{tmdbId}");
    }


    public Task<List<Dictionary<string, object>>?> SearchMoviesAsync(string query)
    {
        return _httpClient.GetFromJsonAsync<List<Dictionary<string, object>>>(
            $"/movies/search?q={Uri.EscapeDataString(query)}", _jsonOptions);
    }

    public async Task<List<Collection>?> GetCollectionsAsync(int movieId)
    {
        return await GetSafeAsync<List<Collection>>($"/movies/{movieId}/collections");
    }

    public async Task<Collection?> GetCollectionAsync(int collectionId)
    {
        return await GetSafeAsync<Collection>($"/collections/{collectionId}");
    }

    public async Task<File?> GetFileAsync(int fileId)
    {
        return await GetSafeAsync<File>($"/files/{fileId}");
    }

    public async Task<File?> PatchFileAsync(int fileId, FileUpdate update)
    {
        var response = await _httpClient.PatchAsync(
            $"/files/{fileId}",
            JsonContent.Create(update, options: _jsonOptions)
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new Exception($"PatchFile failed: {response.StatusCode} {text}");
        }

        return await response.Content.ReadFromJsonAsync<File>(_jsonOptions);
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

    public async Task<Collection?> CreateCollectionAsync(CreateCollectionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/collections", request, _jsonOptions);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<Collection>(_jsonOptions);
        var text = await response.Content.ReadAsStringAsync();
        throw new Exception($"CreateCollection failed: {response.StatusCode} {text}");
    }

    public async Task<Collection?> PatchCollectionAsync(int collectionId, UpdateCollectionRequest update)
    {
        var response = await _httpClient.PatchAsync(
            $"/collections/{collectionId}",
            JsonContent.Create(update, options: _jsonOptions)
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new Exception($"PatchCollection failed: {response.StatusCode} {text}");
        }

        return await response.Content.ReadFromJsonAsync<Collection>(_jsonOptions);
    }

    public async Task<List<DownloadTask>?> GetPendingDownloadsAsync()
    {
        return await GetSafeAsync<List<DownloadTask>>("/downloads/pending");
    }

    public async Task<bool> UpdateDownloadStatusAsync(int taskId, string status, int progress, string? errorMessage = null)
    {
        var payload = new
        {
            status = status,
            progress = progress,
            error_message = errorMessage
        };
        var response = await _httpClient.PostAsJsonAsync($"/downloads/{taskId}/status", payload);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<UploadTask>?> GetPendingUploadsAsync()
    {
        return await GetSafeAsync<List<UploadTask>>("/uploads/pending");
    }

    public async Task<bool> UpdateUploadStatusAsync(int taskId, string status, int progress, string? errorMessage = null)
    {
        var payload = new
        {
            status = status,
            progress = progress,
            error_message = errorMessage
        };
        var response = await _httpClient.PostAsJsonAsync($"/uploads/{taskId}/status", payload);
        return response.IsSuccessStatusCode;
    }
}