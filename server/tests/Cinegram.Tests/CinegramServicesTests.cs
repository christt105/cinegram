using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Health;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot;
using Cinegram.TelegramBot.CallbackQueries;
using Cinegram.TelegramBot.Handlers;
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

    private static Dictionary<string, string?> Telegram() => new()
    {
        ["TELEGRAM_API_ID"] = "12345",
        ["TELEGRAM_API_HASH"] = "0123456789abcdef0123456789abcdef",
        ["TELEGRAM_BOT_TOKEN"] = "123:token",
        ["TELEGRAM_AUTH_USER_ID"] = "111",
    };

    private WebApplicationBuilder Builder(Dictionary<string, string?>? environment)
    {
        var settings = CinegramSettings.Read(name => environment?.GetValueOrDefault(name), _dir);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        builder.Services
            .AddCinegramCore(settings)
            .AddTelegramBot(settings);
        return builder;
    }

    [Fact]
    public void With_the_bot_configured_every_service_and_loop_resolves()
    {
        var environment = Telegram();
        environment["JELLYFIN_BACKUP_DIR"] = _dir;
        environment["JELLYFIN_URL"] = "http://jellyfin.invalid";
        environment["JELLYFIN_TOKEN"] = "token";
        using var app = Builder(environment).Build();

        var names = app.Services.GetServices<IHostedService>()
            .OfType<SupervisedBackgroundService>()
            .Select(s => s.Name)
            .ToList();

        Assert.Equal(
            ["Startup", "Telegram", "Downloader", "Uploader", "WatchedFolder", "WatchResume", "Queue", "Heartbeat", "JellyfinBackup"],
            names);
        Assert.NotNull(app.Services.GetRequiredService<PreviewService>());
        Assert.NotNull(app.Services.GetRequiredService<BotDispatcher>());
        Assert.Equal(TelegramStatus.State.Starting, app.Services.GetRequiredService<TelegramStatus>().Current);
    }

    [Fact]
    public void Every_callback_button_gets_the_services_it_asks_for()
    {
        using var app = Builder(Telegram()).Build();
        var handler = app.Services.GetRequiredService<CallbackQueryHandler>();

        var declared = typeof(CallbackQueryHandler).Assembly.GetTypes()
            .Select(t => t.GetCustomAttribute<CallbackAttribute>()?.Id)
            .OfType<string>()
            .Order()
            .ToList();

        Assert.Empty(handler.Unbound);
        Assert.Equal(declared, handler.Ids.Order());
        Assert.True(declared.Count >= 30);
    }

    [Fact]
    public void The_command_menu_lists_every_command_in_order()
    {
        using var app = Builder(Telegram()).Build();
        var menu = app.Services.GetRequiredService<CommandHandler>().GetMenuCommands().Select(c => c.Command);

        Assert.Equal(
            ["start", "version", "movies", "series", "serie", "movie", "search", "queue", "orphans", "backup",
                "add", "import", "reidentify", "rescan", "auth", "help"],
            menu);
    }

    [Fact]
    public void Without_a_backup_directory_the_Jellyfin_backup_loop_is_left_out()
    {
        using var app = Builder(Telegram()).Build();

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
        HealthEndpoints.Map(api);
        WatchEndpoints.Map(api);
        PreviewEndpoints.Map(api);
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        var health = await http.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using (var body = JsonDocument.Parse(await health.Content.ReadAsStringAsync()))
        {
            Assert.Equal("disabled", body.RootElement.GetProperty("status").GetString());
            Assert.Equal("disabled", body.RootElement.GetProperty("bot").GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("account").ValueKind);
            Assert.Equal(0, body.RootElement.GetProperty("loops").GetArrayLength());
        }

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

    [Fact]
    public void Before_the_bot_starts_the_report_lists_every_bot_loop_waiting()
    {
        var environment = Telegram();
        environment["JELLYFIN_BACKUP_DIR"] = _dir;
        using var app = Builder(environment).Build();

        var report = HealthEndpoints.Read(
            app.Services.GetRequiredService<TelegramStatus>(),
            app.Services.GetServices<IHostedService>(),
            app.Services.GetRequiredService<UserClientService>(),
            app.Services.GetRequiredService<BotHeartbeat>(),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthVerdict.Starting, report.Status);
        Assert.Equal(AccountSession.NotChecked, report.Account!.Session);
        Assert.Equal(["Downloader", "Uploader", "WatchedFolder", "WatchResume", "Queue", "Heartbeat", "JellyfinBackup"],
            report.Loops.Select(l => l.Name));
        Assert.All(report.Loops, l => Assert.Equal(LoopState.Waiting, l.State));
    }

    [Fact]
    public async Task A_bot_that_failed_to_start_turns_the_health_endpoint_into_a_503()
    {
        var failed = new TelegramStatus();
        failed.MarkFailed("401 Unauthorized");
        var builder = Builder(null);
        builder.Services.Replace(ServiceDescriptor.Singleton(failed));
        await using var app = builder.Build();
        HealthEndpoints.Map(app.MapGroup("/api"));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        var health = await http.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        using var body = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("The bot failed to start: 401 Unauthorized",
            body.RootElement.GetProperty("problems")[0].GetString());

        await app.StopAsync();
    }
}
