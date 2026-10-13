using Cinegram;
using Cinegram.Data;
using Cinegram.Services;
using Cinegram.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

if (args.Length > 0 && args[0] == "auth")
    return await ConsoleAuth.RunAsync();

var builder = WebApplication.CreateBuilder(args);
var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? CinegramDb.DefaultPath;
builder.Services.AddDbContextFactory<CinegramDb>(options => options.UseSqlite(CinegramDb.ConnectionString(databasePath)));
builder.Services.AddSingleton<ITmdbLookup>(_ => TmdbIdentifier.FromEnvironment());
builder.Services.AddSingleton<WatchedFiles>();
builder.Services.AddSingleton<TransferTasks>();
builder.Services.AddSingleton<CatalogIdentifier>();
builder.Services.AddSingleton<Catalog>();
builder.Services.AddSingleton<DatabaseBackup>();
builder.Services.AddSingleton<BotHolder>();
builder.Services.AddSingleton(_ => JellyfinProxy.FromEnvironment());
builder.Services.AddHostedService<Worker>();

// Keep Kestrel on a fixed internal port
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

if (!await DatabaseSchema.RunAsync(databasePath, CancellationToken.None))
    return 1;

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = WebCache.Apply });

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

api.MapGet("/version", () => Results.Ok(new { version = AppVersion.Current }));

// Trigger preview of a single movie collection (info card + files)
api.MapPost("/preview/collection/{collectionId:int}", async (int collectionId, IServiceProvider sp) =>
{
    var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
    var (ok, error) = await preview.SendCollectionPreviewAsync(collectionId);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

// Trigger preview of all files in a season
api.MapPost("/preview/series/{seriesId:int}/season/{seasonNumber:int}", async (int seriesId, int seasonNumber, IServiceProvider sp) =>
{
    var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
    var (ok, error) = await preview.SendSeasonPreviewAsync(seriesId, seasonNumber);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

// List the video files sitting in the mounted library directories
api.MapGet("/local/files", (
    string? q,
    [FromQuery(Name = "tmdb_id")] int? tmdbId,
    [FromQuery(Name = "tvdb_id")] int? tvdbId,
    IServiceProvider sp) =>
{
    var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
    return Results.Ok(localMedia.ListFiles(q, tmdbId, tvdbId));
});

// Read a local file with ffprobe and store it as a collection's technical metadata
api.MapPost("/probe/collection/{collectionId:int}", async (int collectionId, ProbeRequest request, IServiceProvider sp) =>
{
    var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
    var (ok, error) = await localMedia.ProbeIntoCollectionAsync(collectionId, request.Path);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

// Match a series' episodes against local files under its library folder and probe the
// unambiguous ones straight into their collection
api.MapPost("/probe/series/{seriesId:int}", async (int seriesId, IServiceProvider sp) =>
{
    var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
    var (ok, error, plan) = await localMedia.MatchSeriesAsync(seriesId);
    return ok ? Results.Ok(plan) : Results.Problem(error);
});

// Watched files in the downloads folder: list, re-identify, confirm/correct and rescan
WatchEndpoints.Map(api);

// Download and upload queues: enqueue, list, cancel and retry
TransferEndpoints.Map(api);

// The library: movies, series, collections, files and TMDB search
CatalogEndpoints.Map(api);

// Database backup, orphaned collections and the forward-origin exposure report
MaintenanceEndpoints.Map(api);

// Delete a collection's downloaded file, keeping the collection itself
api.MapDelete("/local/collection/{collectionId:int}", async (int collectionId, IServiceProvider sp) =>
{
    var localMedia = ActivatorUtilities.CreateInstance<LocalMediaService>(sp);
    var (ok, error) = await localMedia.DeleteLocalCopyAsync(collectionId);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

// Jellyfin reads for the web, with the token kept on the server
JellyfinProxy.Map(api);

api.MapFallback(() => Results.NotFound());

// The web's client-side routes
app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = WebCache.Apply });

await app.RunAsync();
return 0;

record ProbeRequest(string Path);
