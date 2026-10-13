using Microsoft.EntityFrameworkCore;
using Cinegram;
using Cinegram.Backups;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Jellyfin;
using Cinegram.Library;
using Cinegram.TelegramBot;
using Cinegram.Tmdb;
using Cinegram.Transfers;
using Cinegram.Watch;

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

// Telegram previews of a collection or a season
PreviewEndpoints.Map(api);

// Video files in the mounted library: listing, probing into collections and deleting local copies
LocalMediaEndpoints.Map(api);

// Watched files in the downloads folder: list, re-identify, confirm/correct and rescan
WatchEndpoints.Map(api);

// Download and upload queues: enqueue, list, cancel and retry
TransferEndpoints.Map(api);

// The library: movies, series, collections, files and TMDB search
CatalogEndpoints.Map(api);

// Database backup, orphaned collections and the forward-origin exposure report
MaintenanceEndpoints.Map(api);

// Jellyfin reads for the web, with the token kept on the server
JellyfinProxy.Map(api);

api.MapFallback(() => Results.NotFound());

// The web's client-side routes
app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = WebCache.Apply });

await app.RunAsync();
return 0;

