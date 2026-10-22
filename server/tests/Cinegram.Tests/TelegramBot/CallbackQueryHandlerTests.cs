using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Xunit;
using Cinegram.TelegramBot.Handlers;

namespace Cinegram.Tests.TelegramBot;

public class CallbackQueryHandlerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-callbacks-test-").FullName;
    private readonly WebApplication _app;
    private readonly CallbackQueryHandler _handler;

    public CallbackQueryHandlerTests()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TELEGRAM_API_ID"] = "12345",
            ["TELEGRAM_API_HASH"] = "0123456789abcdef0123456789abcdef",
            ["TELEGRAM_BOT_TOKEN"] = "123:token",
            ["TELEGRAM_AUTH_USER_ID"] = "111",
        };
        var settings = CinegramSettings.Read(name => environment.GetValueOrDefault(name), _dir);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddCinegramCore(settings).AddTelegramBot(settings);
        _app = builder.Build();
        _handler = _app.Services.GetRequiredService<CallbackQueryHandler>();
    }

    public void Dispose()
    {
        ((IDisposable)_app).Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private Task<string?> Press(string? data) =>
        _handler.HandleCallbackQueryAsync(new CallbackQuery { Id = "1", Data = data });

    [Fact]
    public async Task A_button_without_data_is_answered_without_a_notice()
    {
        Assert.Null(await Press(null));
    }

    [Fact]
    public async Task An_unknown_button_is_answered_with_a_hint()
    {
        Assert.Equal("This button no longer works.", await Press("gone:1"));
    }

    [Fact]
    public async Task A_button_that_fails_is_answered_with_the_error()
    {
        var notice = await Press("show-file:not-a-number");

        Assert.NotNull(notice);
        Assert.StartsWith("❌", notice);
        Assert.Contains("not-a-number", notice);
        Assert.True(notice.Length <= CallbackQueryHandler.NoticeLimit);
    }

    [Fact]
    public async Task A_button_that_does_nothing_to_report_is_answered_without_a_notice()
    {
        Assert.Null(await Press("queue:refresh"));
    }
}
