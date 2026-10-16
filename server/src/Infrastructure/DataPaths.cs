namespace Cinegram.Infrastructure;

/// <summary>
/// Where Cinegram keeps its own state, all under one data directory (<c>/data</c> in the image):
/// the database, the Telegram sessions, transfer scratch space and the Jellyfin backup bookkeeping.
/// </summary>
public sealed record DataPaths(string Root, string Database)
{
    public const string DefaultRoot = "/data";

    /// <summary>The database lives in <c>db/</c> under the root unless <c>DATABASE_PATH</c> says otherwise.</summary>
    public static DataPaths Read(Func<string, string?> read, string root = DefaultRoot) =>
        new(root, read("DATABASE_PATH") is { Length: > 0 } database ? database : Path.Combine(root, "db", "database.db"));

    public string BotDatabase => Path.Combine(Root, "bot.sqlite");

    public string UserSession => Path.Combine(Root, "user_client.session");

    public string TempDownloads => Path.Combine(Root, "temp", "downloads");

    public string TempUploads => Path.Combine(Root, "temp", "uploads");

    public string TempBackups => Path.Combine(Root, "temp", "backups");

    public string JellyfinBackupLastRun => Path.Combine(Root, "jellyfin-backup-last-run");

    public string JellyfinBackupHistory => Path.Combine(Root, "jellyfin-backup-history.json");
}
