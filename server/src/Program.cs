using Cinegram;
using Cinegram.Data;
using Cinegram.Health;
using Cinegram.Infrastructure;
using Cinegram.Jellyfin;
using Cinegram.Library;
using Cinegram.TelegramBot;
using Cinegram.Transfers;
using Cinegram.Watch;

var settings = CinegramSettings.Read(Environment.GetEnvironmentVariable);

if (args.Length > 0 && args[0] == "auth")
    return await ConsoleAuth.RunAsync(settings);

foreach (var line in settings.Describe())
    Log.Info(line);

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddCinegramCore(settings)
    .AddTelegramBot(settings);

// Keep Kestrel on a fixed internal port
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

if (!await DatabaseSchema.RunAsync(settings.Paths.Database, CancellationToken.None))
    return 1;

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = WebCache.Apply });

var api = app.MapGroup("/api");

// The bot, the account and the background loops; 503 when the bot cannot do its job
HealthEndpoints.Map(api);

api.MapGet("/version", (AppVersion version) => Results.Ok(new { version = version.Current }));

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

