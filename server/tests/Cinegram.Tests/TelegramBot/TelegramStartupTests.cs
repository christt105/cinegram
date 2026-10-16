using Xunit;
using Cinegram.TelegramBot;

namespace Cinegram.Tests.TelegramBot;

public class TelegramStartupTests
{
    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["TELEGRAM_API_ID"] = "12345",
        ["TELEGRAM_API_HASH"] = "hash",
        ["TELEGRAM_BOT_TOKEN"] = "123:token",
        ["TELEGRAM_AUTH_USER_ID"] = "111, 222",
    };

    private static Func<string, string?> Env(Dictionary<string, string?> values) =>
        name => values.GetValueOrDefault(name);

    [Fact]
    public void Read_returns_the_options_when_everything_is_set()
    {
        var (options, problem) = TelegramOptions.Read(Env(Complete));

        Assert.Null(problem);
        Assert.Equal(new TelegramOptions(12345, "hash", "123:token", 111), options);
    }

    [Fact]
    public void Read_names_every_missing_or_malformed_variable()
    {
        var (options, problem) = TelegramOptions.Read(Env(new()
        {
            ["TELEGRAM_API_ID"] = "not-a-number",
            ["TELEGRAM_API_HASH"] = "hash",
            ["TELEGRAM_AUTH_USER_ID"] = "111,abc",
        }));

        Assert.Null(options);
        Assert.Equal("TELEGRAM_API_ID, TELEGRAM_BOT_TOKEN, TELEGRAM_AUTH_USER_ID missing or invalid", problem);
    }

    [Fact]
    public void Read_with_nothing_set_reports_the_bot_as_unconfigured()
    {
        var (options, problem) = TelegramOptions.Read(_ => null);

        Assert.Null(options);
        Assert.StartsWith("TELEGRAM_API_ID, TELEGRAM_API_HASH, TELEGRAM_BOT_TOKEN, TELEGRAM_AUTH_USER_ID", problem);
    }

    [Fact]
    public async Task A_loop_waits_for_the_bot_and_starts_once_it_is_ready()
    {
        var status = new TelegramStatus();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loop = new AfterTelegramStarts("Probe", status, ct =>
        {
            started.TrySetResult();
            return Task.Delay(Timeout.Infinite, ct);
        });

        await loop.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        Assert.False(started.Task.IsCompleted);

        status.MarkReady();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await loop.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_loop_never_starts_when_the_bot_fails_or_is_disabled(bool disabled)
    {
        var status = new TelegramStatus();
        var ran = false;
        var loop = new AfterTelegramStarts("Probe", status, _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        await loop.StartAsync(CancellationToken.None);
        if (disabled) status.MarkDisabled("TELEGRAM_API_ID missing or invalid");
        else status.MarkFailed("login failed");
        await loop.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(ran);
        Assert.False(status.IsReady);
    }
}
