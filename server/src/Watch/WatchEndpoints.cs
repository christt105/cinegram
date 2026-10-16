using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Cinegram.Data;
using Cinegram.TelegramBot;

namespace Cinegram.Watch;

/// <summary>The downloads page's API: the watched files, their re-identification, and confirming
/// or correcting a guess, which moves the file right away.</summary>
public static class WatchEndpoints
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public record ResolveRequest(
        [property: JsonPropertyName("tmdb_id")] int? TmdbId,
        [property: JsonPropertyName("season")] int? Season,
        [property: JsonPropertyName("episode")] int? Episode);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/watch", async (string? status, WatchedFiles watchedFiles, CancellationToken ct) =>
            Results.Json(await watchedFiles.ListAsync(status, ct), Json));

        app.MapPost("/watch/reidentify", async (WatchedFiles watchedFiles, CancellationToken ct) =>
            Results.Json(await watchedFiles.ReidentifyAsync(ct), Json));

        app.MapPost("/watch/{id:int}/confirm", (int id, [FromBody] ResolveRequest? request, WatchedFiles watchedFiles,
                TelegramStatus telegram, [FromServices] WatchNotificationService? notifications,
                IHostApplicationLifetime lifetime) =>
            ResolveAsync(id, request, "confirmed", watchedFiles, telegram, notifications, lifetime));

        app.MapPost("/watch/{id:int}/correct", (int id, [FromBody] ResolveRequest? request, WatchedFiles watchedFiles,
                TelegramStatus telegram, [FromServices] WatchNotificationService? notifications,
                IHostApplicationLifetime lifetime) =>
            ResolveAsync(id, request, "corrected", watchedFiles, telegram, notifications, lifetime));

        app.MapPost("/watch/rescan", async (TelegramStatus telegram, [FromServices] WatchedFolderService? watchedFolder) =>
        {
            if (!telegram.IsReady || watchedFolder is null)
                return Results.Problem("WatchedFolderService not yet initialised.", statusCode: 503);

            try
            {
                var diff = await watchedFolder.RescanAsync(CancellationToken.None);
                return Results.Ok(new { @new = diff.New.Count, missing = diff.Missing.Count });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 503);
            }
        });
    }

    /// <summary>
    /// Settles the identity and answers with it straight away; the move runs in the background, as
    /// it can take minutes across filesystems. Before the bot is up the row stays confirmed or
    /// corrected, and <see cref="WatchNotificationService.ResumeResolvedAsync"/> moves it on startup.
    /// </summary>
    private static async Task<IResult> ResolveAsync(
        int id, ResolveRequest? request, string status, WatchedFiles watchedFiles, TelegramStatus telegram,
        WatchNotificationService? notifications, IHostApplicationLifetime lifetime)
    {
        if (request?.TmdbId is not { } tmdbId) return Error(422, "tmdb_id is required");

        var outcome = await watchedFiles.ResolveAsync(id, tmdbId, request.Season, request.Episode, status);
        switch (outcome.Error)
        {
            case WatchedFiles.ResolveError.NotFound:
            case WatchedFiles.ResolveError.TmdbNotFound:
                return Error(404, outcome.Detail!);
            case WatchedFiles.ResolveError.TmdbFailed:
                return Error(502, outcome.Detail!);
        }

        var resolution = outcome.Resolution!;
        if (telegram.IsReady && notifications is not null)
            _ = Task.Run(() => notifications.MoveResolvedAsync(resolution, lifetime.ApplicationStopping));

        return Results.Json(resolution, Json);
    }

    private static IResult Error(int statusCode, string detail) =>
        Results.Json(new { detail }, Json, statusCode: statusCode);
}
