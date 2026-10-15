using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.Enums;
using Cinegram.Backups;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Jellyfin;
using Cinegram.Library;
using Cinegram.TelegramBot;
using Cinegram.TelegramBot.Commands;
using Cinegram.TelegramBot.Handlers;
using Cinegram.Tmdb;
using Cinegram.Transfers;
using Cinegram.Watch;

namespace Cinegram;

/// <summary>
/// The composition root: every service the web and the bot use, and the background loops.
/// </summary>
public static class CinegramServices
{
    /// <summary>The database and everything the web serves, which never depends on Telegram.</summary>
    public static IServiceCollection AddCinegramCore(this IServiceCollection services, string databasePath)
    {
        services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        services.AddDbContextFactory<CinegramDb>(options => options.UseSqlite(CinegramDb.ConnectionString(databasePath)));
        services.AddSingleton<ITmdbLookup>(_ => TmdbIdentifier.FromEnvironment());
        services.AddSingleton<WatchedFiles>();
        services.AddSingleton<TransferTasks>();
        services.AddSingleton<CatalogIdentifier>();
        services.AddSingleton<Catalog>();
        services.AddSingleton<DatabaseBackup>();
        services.AddSingleton(_ => JellyfinProxy.FromEnvironment());
        services.AddHostedService<StartupChecks>();
        return services;
    }

    /// <summary>
    /// The bot, the account and every loop that works through them. Without <paramref name="options"/>
    /// none of it is registered and <see cref="TelegramStatus"/> says why.
    /// </summary>
    public static IServiceCollection AddTelegramBot(this IServiceCollection services, TelegramOptions? options,
        string? problem, Func<string, string?> readEnvironment)
    {
        if (options is null)
        {
            var disabled = new TelegramStatus();
            disabled.MarkDisabled(problem ?? "not configured");
            services.AddSingleton(disabled);
            Log.Error($"[Telegram] The bot will not start: {problem}. The web keeps working.");
            return services;
        }

        services.AddSingleton<TelegramStatus>();
        services.AddSingleton(options);
        services.AddSingleton(_ => new BotSession(options.BotDatabasePath));
        services.AddSingleton(sp =>
        {
            var bot = new WTelegram.Bot(options.BotToken, options.ApiId, options.ApiHash,
                sp.GetRequiredService<BotSession>().Connection);
            TransferTuning.Apply(bot.Client, "Bot", defaultParallelTransfers: 4);
            return bot;
        });
        services.AddSingleton<UserClientService>();
        services.AddSingleton<TaskQueue>();
        services.AddSingleton<UploadEchoRegistry>();
        services.AddSingleton<WatchedFileMessageRegistry>();
        services.AddSingleton<PendingActionHandler>();

        if (JellyfinClient.IsConfigured(readEnvironment))
            services.AddSingleton<IJellyfinClient>(_ =>
                new JellyfinClient(readEnvironment("JELLYFIN_URL")!, readEnvironment("JELLYFIN_TOKEN")!));
        else
            Log.Warning(JellyfinClient.NotConfiguredWarning);
        services.AddSingleton(sp =>
        {
            var bot = sp.GetRequiredService<WTelegram.Bot>();
            return new JellyfinSeriesIdentifier(
                sp.GetService<IJellyfinClient>(),
                async text => await bot.SendMessage(options.OwnerUserId, text, ParseMode.Html));
        });

        services.AddSingleton<WatchNotificationService>();
        services.AddSingleton<WatchedFolderService>();
        services.AddSingleton<DownloadService>();
        services.AddSingleton<UploadService>();
        services.AddSingleton<PreviewService>();
        AddCommands(services);
        services.AddSingleton<CommandHandler>();
        services.AddSingleton<CallbackQueryHandler>();
        services.AddSingleton<FileHandler>();
        services.AddSingleton<MessageHandler>();
        services.AddSingleton<BotDispatcher>();

        services.AddSingleton<TelegramStartup>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TelegramStartup>());

        AddLoop(services, "Downloader", sp => sp.GetRequiredService<DownloadService>().RunAsync);
        AddLoop(services, "Uploader", sp => sp.GetRequiredService<UploadService>().RunAsync);
        AddLoop(services, "WatchedFolder", sp => sp.GetRequiredService<WatchedFolderService>().RunAsync);
        AddLoop(services, "WatchResume", sp => sp.GetRequiredService<WatchNotificationService>().ResumeResolvedAsync,
            runsUntilShutdown: false);
        AddLoop(services, "Queue", sp => sp.GetRequiredService<TaskQueue>().StartProcessing);

        var backupOptions = JellyfinBackupOptions.FromEnvironment(readEnvironment, options.OwnerUserId);
        if (backupOptions is not null)
        {
            services.AddSingleton(backupOptions);
            services.AddSingleton<JellyfinBackupService>();
            AddLoop(services, "JellyfinBackup", sp => sp.GetRequiredService<JellyfinBackupService>().RunAsync);
        }
        else
        {
            Log.Info("[JellyfinBackup] Disabled: set JELLYFIN_BACKUP_DIR to enable periodic backups.");
        }

        return services;
    }

    /// <summary>The bot's commands, in the order the "/" menu and /help list them.</summary>
    private static void AddCommands(IServiceCollection services)
    {
        services.AddSingleton<ICommand, StartCommand>();
        services.AddSingleton<ICommand, VersionCommand>();
        services.AddSingleton<ICommand, MoviesCommand>();
        services.AddSingleton<ICommand, SeriesCommand>();
        services.AddSingleton<ICommand, SerieCommand>();
        services.AddSingleton<ICommand, MovieCommand>();
        services.AddSingleton<ICommand, SearchCommand>();
        services.AddSingleton<ICommand, QueueCommand>();
        services.AddSingleton<ICommand, OrphansCommand>();
        services.AddSingleton<ICommand, BackupCommand>();
        services.AddSingleton<ICommand, AddCommand>();
        services.AddSingleton<ICommand, ImportCommand>();
        services.AddSingleton<ICommand, ReidentifyWatchedFilesCommand>();
        services.AddSingleton<ICommand, RescanDownloadsCommand>();
        services.AddSingleton<ICommand, AuthCommand>();
    }

    /// <summary>
    /// Registered as plain <see cref="IHostedService"/> singletons: AddHostedService would keep only
    /// the first of several factories returning the same type.
    /// </summary>
    private static void AddLoop(IServiceCollection services, string name,
        Func<IServiceProvider, Func<CancellationToken, Task>> run, bool runsUntilShutdown = true) =>
        services.AddSingleton<IHostedService>(sp =>
            new AfterTelegramStarts(name, sp.GetRequiredService<TelegramStatus>(), run(sp), runsUntilShutdown));
}

/// <summary>The bot's own state database, kept open for the life of the process.</summary>
public sealed class BotSession : IDisposable
{
    public BotSession(string path) => Connection = new SqliteConnection($"Data Source={path}");

    public SqliteConnection Connection { get; }

    public void Dispose() => Connection.Dispose();
}
