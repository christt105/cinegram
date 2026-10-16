using Cinegram.Infrastructure;
using Cinegram.TelegramBot;

namespace Cinegram.Health;

/// <summary>
/// The state of the bot, the account and every loop that works through them, with the verdict
/// the container's health check acts on. Only what stops the bot from doing its job counts as a
/// problem: a bot disabled on purpose or an account without a saved session do not.
/// </summary>
public sealed record HealthReport(
    HealthVerdict Status,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Warnings,
    BotReport Bot,
    AccountReport? Account,
    IReadOnlyList<LoopReport> Loops)
{
    /// <summary>How long the bot may take to start before that counts as a problem.</summary>
    public static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(5);

    public bool Healthy => Problems.Count == 0;

    public static HealthReport Build(BotReport bot, AccountReport? account, IReadOnlyList<LoopReport> loops,
        DateTimeOffset now)
    {
        var problems = new List<string>();
        var warnings = new List<string>();

        switch (bot.State)
        {
            case TelegramStatus.State.Failed:
                problems.Add($"The bot failed to start: {bot.Problem}");
                break;
            case TelegramStatus.State.Starting when now - bot.Since > StartupGrace:
                problems.Add($"The bot has been starting for more than {StartupGrace.TotalMinutes:0} minutes");
                break;
            case TelegramStatus.State.Ready when bot.Connected == false:
                problems.Add("The bot lost its connection to Telegram");
                break;
            case TelegramStatus.State.Disabled:
                warnings.Add($"The bot is disabled: {bot.Problem}");
                break;
        }

        foreach (var loop in loops)
        {
            if (loop.State == LoopState.Crashed)
                problems.Add($"{loop.Name} stopped by an error: {loop.Problem}");
            else if (loop.State == LoopState.Stopped)
                problems.Add($"{loop.Name} stopped before shutdown");
        }

        if (account?.Session == AccountSession.Missing)
            warnings.Add("No saved account session: transfers go through the bot alone");
        else if (account?.Session == AccountSession.Failed)
            warnings.Add($"The account session could not be resumed: {account.Problem}");

        var status = problems.Count > 0 ? HealthVerdict.Unhealthy
            : bot.State == TelegramStatus.State.Starting ? HealthVerdict.Starting
            : bot.State == TelegramStatus.State.Disabled ? HealthVerdict.Disabled
            : HealthVerdict.Ok;

        return new HealthReport(status, problems, warnings, bot, account, loops);
    }
}

public enum HealthVerdict
{
    Ok,
    Starting,
    /// <summary>The bot is off on purpose (no Telegram settings); the web works.</summary>
    Disabled,
    Unhealthy,
}

/// <param name="Connected">Whether the bot's connection to Telegram is up; null until it starts.</param>
public sealed record BotReport(TelegramStatus.State State, string? Problem, DateTimeOffset Since, bool? Connected);

public sealed record AccountReport(AccountSession Session, bool Premium, string? Problem);

public sealed record LoopReport(string Name, LoopState State, string? Problem, DateTimeOffset Since);
