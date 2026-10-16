using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot;
using Cinegram.Watch;

namespace Cinegram.Tests;

public class CinegramServicesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-services-test-").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private WebApplicationBuilder Builder(TelegramOptions? telegram, Dictionary<string, string?>? environment = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        builder.Services
            .AddCinegramCore(Path.Combine(_dir, "database.db"))
            .AddTelegramBot(telegram, telegram is null ? "TELEGRAM_API_ID missing or invalid" : null,
                name => environment?.GetValueOrDefault(name));
        return builder;
    }

    [Fact]
    public void With_the_bot_configured_every_service_and_loop_resolves()
    {
        var telegram = new TelegramOptions(12345, "0123456789abcdef0123456789abcdef", "123:token", 111, Path.Combine(_dir, "bot.sqlite"));
        using var app = Builder(telegram, new()
        {
            ["JELLYFIN_BACKUP_DIR"] = _dir,
            ["JELLYFIN_URL"] = "http://jellyfin.invalid",
            ["JELLYFIN_TOKEN"] = "token",
        }).Build();

        var names = app.Services.GetServices<IHostedService>()
            .OfType<SupervisedBackgroundService>()
            .Select(s => s.Name)
            .ToList();

        Assert.Equal(
            ["Startup", "Telegram", "Downloader", "Uploader", "WatchedFolder", "WatchResume", "Queue", "JellyfinBackup"],
            names);
        Assert.NotNull(app.Services.GetRequiredService<PreviewService>());
        Assert.NotNull(app.Services.GetRequiredService<BotDispatcher>());
        Assert.Same(
            app.Services.GetRequiredService<WatchedFolderService>(),
            app.Services.GetRequiredService<BotDispatcher>().WatchedFolder);
        Assert.Equal(TelegramStatus.State.Starting, app.Services.GetRequiredService<TelegramStatus>().Current);
    }

    [Fact]
    public void Without_a_backup_directory_the_Jellyfin_backup_loop_is_left_out()
    {
        var telegram = new TelegramOptions(12345, "0123456789abcdef0123456789abcdef", "123:token", 111, Path.Combine(_dir, "bot.sqlite"));
        using var app = Builder(telegram).Build();

        var names = app.Services.GetServices<IHostedService>()
            .OfType<SupervisedBackgroundService>()
            .Select(s => s.Name);

        Assert.DoesNotContain("JellyfinBackup", names);
    }

    [Fact]
    public async Task Without_the_bot_the_web_serves_and_bot_actions_answer_that_it_is_not_up()
    {
        var builder = Builder(null);
        await using var app = builder.Build();
        var api = app.MapGroup("/api");
        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        WatchEndpoints.Map(api);
        PreviewEndpoints.Map(api);
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/health")).StatusCode);

        var rescan = await http.PostAsync("/api/watch/rescan", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, rescan.StatusCode);

        var preview = await http.PostAsync("/api/preview/collection/1", null);
        Assert.Equal(HttpStatusCode.InternalServerError, preview.StatusCode);
        using var problem = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal(PreviewService.NotReady, problem.RootElement.GetProperty("detail").GetString());

        var status = app.Services.GetRequiredService<TelegramStatus>();
        Assert.Equal(TelegramStatus.State.Disabled, status.Current);
        Assert.Equal(["Startup"], app.Services.GetServices<IHostedService>()
            .OfType<SupervisedBackgroundService>().Select(s => s.Name));

        await app.StopAsync();
    }
}
