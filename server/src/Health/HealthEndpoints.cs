using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Cinegram.TelegramBot;

namespace Cinegram.Health;

/// <summary>
/// <c>GET /health</c>: the <see cref="HealthReport"/>, answered with 503 when it has a problem so
/// the container's health check fails with it.
/// </summary>
public static class HealthEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (TelegramStatus status, IEnumerable<IHostedService> hosted,
            [FromServices] UserClientService? account, [FromServices] WTelegram.Bot? bot) =>
        {
            var report = Read(status, hosted, account, bot, DateTimeOffset.UtcNow);
            return Results.Json(report, JsonOptions,
                statusCode: report.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }

    public static HealthReport Read(TelegramStatus status, IEnumerable<IHostedService> hosted,
        UserClientService? account, WTelegram.Bot? bot, DateTimeOffset now)
    {
        var botReport = new BotReport(status.Current, status.Problem, status.Since,
            status.IsReady && bot is not null ? !bot.Client.Disconnected : null);

        var accountReport = account is null
            ? null
            : new AccountReport(account.Session, account.IsPremium, account.SessionProblem);

        var loops = hosted.OfType<AfterTelegramStarts>()
            .Select(loop =>
            {
                var current = loop.Status;
                return new LoopReport(loop.Name, current.State, current.Problem, current.Since);
            })
            .ToList();

        return HealthReport.Build(botReport, accountReport, loops, now);
    }
}
