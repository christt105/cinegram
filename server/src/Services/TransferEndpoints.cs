using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Cinegram.Services;

/// <summary>The transfers API: queueing downloads and uploads, the queue views, and cancelling or
/// retrying a task. Queueing wakes the matching service straight away.</summary>
public static class TransferEndpoints
{
    public record DownloadEnqueueRequest([property: JsonPropertyName("name_suffix")] string? NameSuffix);

    public record UploadEnqueueRequest(
        [property: JsonPropertyName("jellyfin_id")] string? JellyfinId,
        [property: JsonPropertyName("tmdb_id")] int? TmdbId,
        [property: JsonPropertyName("media_type")] string? MediaType,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("year")] int? Year);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/tasks/downloads", async (TransferTasks tasks, CancellationToken ct) =>
            Json(await tasks.ListDownloadsAsync(ct)));

        app.MapGet("/tasks/uploads", async (TransferTasks tasks, CancellationToken ct) =>
            Json(await tasks.ListUploadsAsync(ct)));

        app.MapDelete("/tasks/completed", async (TransferTasks tasks, CancellationToken ct) =>
        {
            var (downloads, uploads) = await tasks.ClearCompletedAsync(ct);
            return Json(new { status = "ok", cleared_downloads = downloads, cleared_uploads = uploads });
        });

        app.MapPost("/downloads/enqueue/collection/{collectionId:int}",
            async (int collectionId, HttpRequest http, TransferTasks tasks, CancellationToken ct) =>
            {
                DownloadEnqueueRequest? request;
                try
                {
                    request = await ReadOptionalJsonAsync<DownloadEnqueueRequest>(http, ct);
                }
                catch (JsonException)
                {
                    return Error(422, "Invalid JSON body");
                }

                return await tasks.EnqueueCollectionAsync(collectionId, request?.NameSuffix, ct)
                    ? Status("ok", "Enqueued collection")
                    : Status("ignored", "Collection already in queue or empty");
            });

        app.MapPost("/downloads/enqueue/series/{seriesId:int}",
            async (int seriesId, TransferTasks tasks, CancellationToken ct) =>
                Enqueued(await tasks.EnqueueSeriesAsync(seriesId, ct), count => $"Enqueued {count} collections for series"));

        app.MapPost("/downloads/enqueue/series/{seriesId:int}/season/{seasonNumber:int}",
            async (int seriesId, int seasonNumber, TransferTasks tasks, CancellationToken ct) =>
                Enqueued(await tasks.EnqueueSeasonAsync(seriesId, seasonNumber, ct),
                    count => $"Enqueued {count} collections for season {seasonNumber}"));

        app.MapPost("/downloads/enqueue/series/{seriesId:int}/season/{seasonNumber:int}/episode/{episodeNumber:int}",
            async (int seriesId, int seasonNumber, int episodeNumber, TransferTasks tasks, CancellationToken ct) =>
            {
                var outcome = await tasks.EnqueueEpisodeAsync(seriesId, seasonNumber, episodeNumber, ct);
                if (outcome.Error == TransferTasks.EnqueueError.None && outcome.Count == 0)
                    return Status("ignored", "Episode collection already enqueued or not found");
                return Enqueued(outcome, _ => "Enqueued episode");
            });

        app.MapGet("/downloads/queue", async (TransferTasks tasks, CancellationToken ct) =>
            Json(await tasks.DownloadQueueAsync(ct)));

        app.MapDelete("/downloads/{taskId:int}", async (int taskId, TransferTasks tasks, CancellationToken ct) =>
            await tasks.DeleteDownloadAsync(taskId, ct) ? Status("ok") : Error(404, "Task not found"));

        app.MapPost("/downloads/{taskId:int}/retry", async (int taskId, TransferTasks tasks, CancellationToken ct) =>
            await tasks.RetryDownloadAsync(taskId, ct) ? Status("ok") : Error(404, "Download task not found"));

        app.MapPost("/uploads/enqueue", async ([FromBody] UploadEnqueueRequest? request, TransferTasks tasks, CancellationToken ct) =>
        {
            if (request is not { JellyfinId: { } jellyfinId, MediaType: { } mediaType, Path: { } path, Title: { } title })
                return Error(422, "jellyfin_id, media_type, path and title are required");

            var queued = await tasks.EnqueueUploadAsync(
                new TransferTasks.UploadRequest(jellyfinId, request.TmdbId, mediaType, path, title, request.Year), ct);
            return queued ? Status("ok") : Status("ignored", "Already in upload queue");
        });

        app.MapGet("/uploads/queue", async (TransferTasks tasks, CancellationToken ct) =>
            Json(await tasks.UploadQueueAsync(ct)));

        app.MapDelete("/uploads/{taskId:int}", async (int taskId, TransferTasks tasks, CancellationToken ct) =>
            await tasks.DeleteUploadAsync(taskId, ct) ? Status("ok") : Error(404, "Task not found"));

        app.MapPost("/uploads/{taskId:int}/retry", async (int taskId, TransferTasks tasks, CancellationToken ct) =>
            await tasks.RetryUploadAsync(taskId, ct) ? Status("ok") : Error(404, "Upload task not found"));
    }

    /// <summary>The body is optional and, as in the backend, read as JSON whatever its content type.</summary>
    private static async Task<T?> ReadOptionalJsonAsync<T>(HttpRequest http, CancellationToken ct) where T : class
    {
        using var reader = new StreamReader(http.Body);
        var body = await reader.ReadToEndAsync(ct);
        return string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body);
    }

    private static IResult Enqueued(TransferTasks.EnqueueOutcome outcome, Func<int, string> message) =>
        outcome.Error switch
        {
            TransferTasks.EnqueueError.SeriesNotFound => Error(404, "Series not found"),
            TransferTasks.EnqueueError.SeasonNotFound => Error(404, "Season not found"),
            TransferTasks.EnqueueError.EpisodeNotFound => Error(404, "Episode not found"),
            _ => Status("ok", message(outcome.Count)),
        };

    private static IResult Status(string status, string? message = null) =>
        message is null ? Json(new { status }) : Json(new { status, message });

    private static IResult Json(object value) => Results.Json(value, WatchEndpoints.Json);

    private static IResult Error(int statusCode, string detail) =>
        Results.Json(new { detail }, WatchEndpoints.Json, statusCode: statusCode);
}
