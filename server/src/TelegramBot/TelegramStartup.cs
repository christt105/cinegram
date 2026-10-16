using Cinegram.Infrastructure;
using Cinegram.Transfers;

namespace Cinegram.TelegramBot;

/// <summary>
/// Brings the bot up: connects and starts taking updates, resumes the account's saved session and
/// puts back the transfers a restart cut off. Only then is <see cref="TelegramStatus"/> ready, so
/// no queue claims a task before that recovery.
/// </summary>
public sealed class TelegramStartup : SupervisedBackgroundService
{
    private readonly WTelegram.Bot _bot;
    private readonly BotDispatcher _dispatcher;
    private readonly UserClientService _userClient;
    private readonly TransferTasks _transfers;
    private readonly TelegramOptions _options;
    private readonly TelegramStatus _status;

    public TelegramStartup(WTelegram.Bot bot, BotDispatcher dispatcher, UserClientService userClient,
        TransferTasks transfers, TelegramOptions options, TelegramStatus status)
    {
        _bot = bot;
        _dispatcher = dispatcher;
        _userClient = userClient;
        _transfers = transfers;
        _options = options;
        _status = status;
    }

    public override string Name => "Telegram";

    protected override bool RunsUntilShutdown => false;

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _dispatcher.InitBot();

            var me = await _bot.GetMe();
            if (!await _userClient.TryResumeSessionAsync(_options.ApiId, _options.ApiHash, me.Username!))
                Log.Info($"[Startup] No saved user session. Authenticate with `{UserClientService.ReauthInstructions}`.");

            var (restartedDownloads, failedUploads) = await _transfers.RecoverInterruptedAsync(stoppingToken);
            if (restartedDownloads + failedUploads > 0)
                Log.Info($"[Startup] A restart cut off {restartedDownloads} download(s), queued again, and {failedUploads} upload(s), marked failed.");
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            Log.Error("Failed to run the bot", ex);
            _status.MarkFailed(ex.Message);
            return;
        }

        _status.MarkReady();
    }
}

/// <summary>
/// A background loop that needs the bot: it starts once <see cref="TelegramStartup"/> is done, and
/// not at all when the bot is disabled or failed to start.
/// </summary>
public sealed class AfterTelegramStarts : SupervisedBackgroundService
{
    private readonly TelegramStatus _status;
    private readonly Func<CancellationToken, Task> _run;
    private readonly bool _runsUntilShutdown;

    public AfterTelegramStarts(string name, TelegramStatus status, Func<CancellationToken, Task> run,
        bool runsUntilShutdown = true)
    {
        Name = name;
        _status = status;
        _run = run;
        _runsUntilShutdown = runsUntilShutdown;
    }

    public override string Name { get; }

    protected override bool RunsUntilShutdown => _runsUntilShutdown;

    protected override Task<bool> WaitToStartAsync(CancellationToken stoppingToken) =>
        _status.WaitUntilSettledAsync(stoppingToken);

    protected override Task RunAsync(CancellationToken stoppingToken) => _run(stoppingToken);
}
