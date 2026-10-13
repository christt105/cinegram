using System.Text.Json.Serialization;
using Bot.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Bot.Services;

/// <summary>The web's maintenance API: database backup, orphaned collections and the report of
/// where the archive's files were forwarded from.</summary>
public static class MaintenanceEndpoints
{
    public sealed record OrphanFileOut(int Id, string Filename, long Filesize);

    public sealed record OrphanOut(int Id, string? Name, string? Quality, int FilesCount, List<OrphanFileOut> Files);

    public sealed record BatchResult(int CollectionId, string Status, string? Error = null);

    public record BatchRequest(
        [property: JsonPropertyName("collection_ids")] List<int>? CollectionIds,
        [property: JsonPropertyName("tmdb_id")] int? TmdbId);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/maintenance/backup", async (DatabaseBackup backup, CancellationToken ct) =>
        {
            try
            {
                return Results.File(await backup.OpenSnapshotAsync(ct), "application/octet-stream",
                    DatabaseBackup.FileName(DateTime.UtcNow));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log.Error("[Maintenance] Database backup failed", ex);
                return Error(500, "Backup failed");
            }
        });

        app.MapGet("/maintenance/orphans", async (Catalog catalog, CancellationToken ct) =>
            Json((await catalog.ListOrphansAsync(ct)).Select(c => new OrphanOut(c.Id, c.Name, c.Quality, c.Files.Count,
                c.Files.Select(f => new OrphanFileOut(f.Id, f.Filename, f.Filesize)).ToList()))));

        app.MapPost("/maintenance/identify-batch", async ([FromBody] BatchRequest? request, CatalogIdentifier identifier,
            CancellationToken ct) =>
        {
            if (request is not { CollectionIds: { } ids, TmdbId: { } tmdbId })
                return Error(422, "collection_ids and tmdb_id are required");

            var results = new List<BatchResult>();
            foreach (var id in ids)
            {
                try
                {
                    results.Add(await identifier.IdentifyAsync(id, new CatalogIdentifier.Request(tmdbId), ct)
                        ? new BatchResult(id, "success")
                        : new BatchResult(id, "error", "Collection not found or TMDB id not found"));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    Log.Error($"[Maintenance] Failed to identify collection {id} in batch", ex);
                    results.Add(new BatchResult(id, "error", "TMDB request failed"));
                }
            }

            return Json(new { status = "ok", results });
        });

        app.MapPost("/maintenance/delete-batch", async ([FromBody] BatchRequest? request, Catalog catalog,
            CancellationToken ct) =>
        {
            if (request is not { CollectionIds: { } ids }) return Error(422, "collection_ids is required");

            await catalog.DeleteCollectionsAsync(ids, ct);
            return Json(new { status = "ok" });
        });

        app.MapGet("/maintenance/forward-sources", async (Catalog catalog, CancellationToken ct) =>
        {
            var sources = await catalog.ListForwardSourcesAsync(ct);
            return Json(new { sources, totalFiles = sources.Sum(s => s.Files), totalSize = sources.Sum(s => s.Size) });
        });
    }

    private static IResult Json(object value) => Results.Json(value, WatchEndpoints.Json);

    private static IResult Error(int statusCode, string detail) =>
        Results.Json(new { detail }, WatchEndpoints.Json, statusCode: statusCode);
}
