using Xunit;
using Cinegram.Health;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot;

namespace Cinegram.Tests.Health;

public class HealthReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static BotReport Bot(TelegramStatus.State state, string? problem = null, TimeSpan? silentFor = null,
        TimeSpan? age = null) =>
        new(state, problem, Now - (age ?? TimeSpan.FromSeconds(10)),
            state == TelegramStatus.State.Ready ? Now - (silentFor ?? TimeSpan.FromSeconds(30)) : null);

    private static LoopReport Loop(string name, LoopState state = LoopState.Running, string? problem = null) =>
        new(name, state, problem, Now);

    private static readonly AccountReport Restored = new(AccountSession.Restored, true, null);

    private static HealthReport Build(BotReport bot, AccountReport? account = null, params LoopReport[] loops) =>
        HealthReport.Build(bot, account, loops, Now);

    [Fact]
    public void A_ready_bot_with_every_loop_running_is_ok()
    {
        var report = Build(Bot(TelegramStatus.State.Ready), Restored,
            Loop("Downloader"), Loop("WatchResume", LoopState.Done));

        Assert.Equal(HealthVerdict.Ok, report.Status);
        Assert.True(report.Healthy);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void A_bot_disabled_on_purpose_keeps_the_container_healthy()
    {
        var report = Build(Bot(TelegramStatus.State.Disabled, "TELEGRAM_BOT_TOKEN missing or invalid"));

        Assert.Equal(HealthVerdict.Disabled, report.Status);
        Assert.True(report.Healthy);
        Assert.Equal(["The bot is disabled: TELEGRAM_BOT_TOKEN missing or invalid"], report.Warnings);
    }

    [Fact]
    public void A_bot_that_failed_to_start_is_a_problem_and_its_skipped_loops_are_not_repeated()
    {
        var report = Build(Bot(TelegramStatus.State.Failed, "401 Unauthorized"), null,
            Loop("Downloader", LoopState.Skipped), Loop("Uploader", LoopState.Skipped));

        Assert.Equal(HealthVerdict.Unhealthy, report.Status);
        Assert.False(report.Healthy);
        Assert.Equal(["The bot failed to start: 401 Unauthorized"], report.Problems);
    }

    [Fact]
    public void A_bot_still_starting_is_fine_until_the_grace_period_runs_out()
    {
        var early = Build(Bot(TelegramStatus.State.Starting, age: TimeSpan.FromMinutes(1)), null, Loop("Downloader", LoopState.Waiting));
        var late = Build(Bot(TelegramStatus.State.Starting, age: HealthReport.StartupGrace + TimeSpan.FromSeconds(1)));

        Assert.Equal(HealthVerdict.Starting, early.Status);
        Assert.True(early.Healthy);
        Assert.Equal(HealthVerdict.Unhealthy, late.Status);
        Assert.Single(late.Problems);
    }

    [Fact]
    public void A_ready_bot_is_answering_until_telegram_stays_silent_past_the_limit()
    {
        var quiet = Build(Bot(TelegramStatus.State.Ready, silentFor: HealthReport.SilenceLimit), Restored);
        var silent = Build(Bot(TelegramStatus.State.Ready, silentFor: HealthReport.SilenceLimit + TimeSpan.FromSeconds(1)), Restored);

        Assert.True(quiet.Bot.Answering);
        Assert.True(quiet.Healthy);
        Assert.False(silent.Bot.Answering);
        Assert.Equal(["Telegram has not answered the bot since 2026-10-16 11:56:59 UTC"], silent.Problems);
    }

    [Fact]
    public void Answering_is_unknown_until_the_bot_is_up()
    {
        var starting = Build(Bot(TelegramStatus.State.Starting));
        var readyWithoutHeartbeat = Build(new BotReport(TelegramStatus.State.Ready, null, Now, null));

        Assert.Null(starting.Bot.Answering);
        Assert.Null(readyWithoutHeartbeat.Bot.Answering);
        Assert.True(readyWithoutHeartbeat.Healthy);
    }

    [Fact]
    public void A_crashed_or_stopped_loop_is_a_problem()
    {
        var report = Build(Bot(TelegramStatus.State.Ready), Restored,
            Loop("Downloader", LoopState.Crashed, "disk full"), Loop("Queue", LoopState.Stopped, "Stopped before shutdown"),
            Loop("Uploader"));

        Assert.Equal(HealthVerdict.Unhealthy, report.Status);
        Assert.Equal(["Downloader stopped by an error: disk full", "Queue stopped before shutdown"], report.Problems);
    }

    [Theory]
    [InlineData(AccountSession.Missing, "No saved account session: transfers go through the bot alone")]
    [InlineData(AccountSession.Failed, "The account session could not be resumed: AUTH_KEY_UNREGISTERED")]
    public void An_account_without_a_session_is_only_a_warning(AccountSession session, string warning)
    {
        var report = Build(Bot(TelegramStatus.State.Ready),
            new AccountReport(session, false, session == AccountSession.Failed ? "AUTH_KEY_UNREGISTERED" : null));

        Assert.Equal(HealthVerdict.Ok, report.Status);
        Assert.Equal([warning], report.Warnings);
    }
}
