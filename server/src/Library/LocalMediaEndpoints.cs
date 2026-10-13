using Microsoft.AspNetCore.Mvc;

namespace Cinegram.Library;

/// <summary>The video files in the mounted library directories: listing them, probing them into a
/// collection's technical metadata and deleting a collection's downloaded copy.</summary>
public static class LocalMediaEndpoints
{
    public record ProbeRequest(string Path);

    public static void Map(IEndpointRouteBuilder app)
    {
        // List the video files sitting in the mounted library directories
        app.MapGet("/local/files", (
            string? q,
            [FromQuery(Name = "tmdb_id")] int? tmdbId,
            [FromQuery(Name = "tvdb_id")] int? tvdbId,
            IServiceProvider sp) =>
        {
            var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
            return Results.Ok(localMedia.ListFiles(q, tmdbId, tvdbId));
        });

        // Read a local file with ffprobe and store it as a collection's technical metadata
        app.MapPost("/probe/collection/{collectionId:int}", async (int collectionId, ProbeRequest request, IServiceProvider sp) =>
        {
            var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
            var (ok, error) = await localMedia.ProbeIntoCollectionAsync(collectionId, request.Path);
            return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
        });

        // Match a series' episodes against local files under its library folder and probe the
        // unambiguous ones straight into their collection
        app.MapPost("/probe/series/{seriesId:int}", async (int seriesId, IServiceProvider sp) =>
        {
            var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
            var (ok, error, plan) = await localMedia.MatchSeriesAsync(seriesId);
            return ok ? Results.Ok(plan) : Results.Problem(error);
        });

        // Delete a collection's downloaded file, keeping the collection itself
        app.MapDelete("/local/collection/{collectionId:int}", async (int collectionId, IServiceProvider sp) =>
        {
            var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
            var (ok, error) = await localMedia.DeleteLocalCopyAsync(collectionId);
            return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
        });
    }
}
