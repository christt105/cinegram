using TL;
using Cinegram.Infrastructure;

namespace Cinegram.TelegramBot;

/// <summary>
/// Pings Telegram through the bot's connection every minute and remembers when it last answered.
/// WTelegram's own keep-alive waits on its ping with no deadline, so a dead connection still looks
/// connected until TCP gives up, about 15 minutes later; this is what the health report reads.
/// </summary>
public sealed class BotHeartbeat
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly WTelegram.Bot _bot;
    private long _lastAnswerTicks;

    public BotHeartbeat(WTelegram.Bot bot) => _bot = bot;

    /// <summary>When Telegram last answered; null before the bot is up.</summary>
    public DateTimeOffset? LastAnswer
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastAnswerTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        MarkAnswered();
        var pingId = Random.Shared.NextInt64();
        var silent = false;

        while (true)
        {
            await Task.Delay(Interval, stoppingToken);
            try
            {
                await _bot.Client.Ping(pingId++).WaitAsync(Timeout, stoppingToken);
                MarkAnswered();
                if (silent)
                    Log.Info("[Heartbeat] Telegram answers again.");
                silent = false;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (!silent)
                    Log.Warning($"[Heartbeat] Telegram did not answer a ping: {ex.Message}");
                silent = true;
            }
        }
    }

    private void MarkAnswered() => Interlocked.Exchange(ref _lastAnswerTicks, DateTimeOffset.UtcNow.UtcTicks);
}
