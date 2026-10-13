using Bot.Services;
using Bot.Utils;
using Microsoft.Data.Sqlite;

namespace Bot;

public class Worker : BackgroundService
{
    private readonly TaskQueue _queue = new();
    private readonly BotHolder _botHolder;
    private readonly WatchedFiles _watchedFiles;
    private readonly TransferTasks _transfers;
    private readonly Catalog _catalog;
    private readonly CatalogIdentifier _identifier;
    private readonly ITmdbLookup _tmdb;
    private readonly DatabaseBackup _backup;

    public Worker(BotHolder botHolder, WatchedFiles watchedFiles, TransferTasks transfers, Catalog catalog,
        CatalogIdentifier identifier, ITmdbLookup tmdb, DatabaseBackup backup)
    {
        _botHolder = botHolder;
        _watchedFiles = watchedFiles;
        _transfers = transfers;
        _catalog = catalog;
        _identifier = identifier;
        _tmdb = tmdb;
        _backup = backup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!Bot.Utils.DependencyChecker.CheckExecutables(out var missing))
            {
                Log.Warning($"[Startup] Required system dependencies are missing: {string.Join(", ", missing)}. Make sure they are installed and available in the PATH.");
            }

            _ = JellyfinPathAudit.RunAsync(stoppingToken);

            var apiId = int.Parse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID")!);
            var apiHash = Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")!;
            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;
            var authUserId = AuthConfig.OwnerUserId;

            await using var connection = new SqliteConnection(@"Data Source=/data/bot.sqlite");
            using var userClient = new UserClientService();
            using var jellyfinClient = JellyfinClient.FromEnvironment();

            var bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);
            TransferTuning.Apply(bot.Client, "Bot", defaultParallelTransfers: 4);

            // Register so PreviewService and HTTP endpoints can use the live bot instance
            _botHolder.Register(bot, authUserId);

            var botDispatcher = new BotDispatcher(bot, _catalog, _identifier, _tmdb, _backup, _watchedFiles, _transfers, _queue, userClient, jellyfinClient);

            await botDispatcher.InitBot();

            // Try to resume a saved user session (non-fatal if unavailable)
            var me = await bot.GetMe();
            if (!await userClient.TryResumeSessionAsync(apiId, apiHash, me.Username!))
                Log.Info($"[Startup] No saved user session. Authenticate with `{UserClientService.ReauthInstructions}`.");

            var (restartedDownloads, failedUploads) = await _transfers.RecoverInterruptedAsync(stoppingToken);
            if (restartedDownloads + failedUploads > 0)
                Log.Info($"[Startup] A restart cut off {restartedDownloads} download(s), queued again, and {failedUploads} upload(s), marked failed.");

            var downloadService = new DownloadService(bot, _catalog, _transfers, _queue, userClient);
            _ = downloadService.RunAsync(stoppingToken);

            var uploadService = new UploadService(bot, _catalog, _transfers, _queue, userClient, botDispatcher.UploadEchoes);
            _ = uploadService.RunAsync(stoppingToken);

            var jellyfinBackupOptions = JellyfinBackupOptions.FromEnvironment(
                Environment.GetEnvironmentVariable, authUserId);
            if (jellyfinBackupOptions is not null)
            {
                var jellyfinBackupService = new JellyfinBackupService(bot, uploadService, jellyfinBackupOptions);
                _ = jellyfinBackupService.RunAsync(stoppingToken);
            }
            else
            {
                Log.Info("[JellyfinBackup] Disabled: set JELLYFIN_BACKUP_DIR to enable periodic backups.");
            }

            _botHolder.RegisterWatch(botDispatcher.WatchedFolder, botDispatcher.WatchNotifications);
            _ = botDispatcher.WatchedFolder.RunAsync(stoppingToken);
            _ = botDispatcher.WatchNotifications.ResumeResolvedAsync(stoppingToken);

            _ = _queue.StartProcessing(stoppingToken);

            while (!stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to run the bot", ex);
        }
    }
}