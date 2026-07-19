using Bot;
using Bot.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<BotHolder>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Keep Kestrel on a fixed internal port
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.UseCors();

// Health check
app.MapGet("/", () => Results.Ok(new { status = "ok" }));

// Trigger preview of a single movie collection (info card + files)
app.MapPost("/preview/collection/{collectionId:int}", async (int collectionId, IServiceProvider sp) =>
{
    var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
    var (ok, error) = await preview.SendCollectionPreviewAsync(collectionId);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

// Trigger preview of all files in a season
app.MapPost("/preview/series/{seriesId:int}/season/{seasonNumber:int}", async (int seriesId, int seasonNumber, IServiceProvider sp) =>
{
    var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
    var (ok, error) = await preview.SendSeasonPreviewAsync(seriesId, seasonNumber);
    return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
});

await app.RunAsync();
