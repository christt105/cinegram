using Cinegram.Backups;
using Cinegram.Infrastructure;
using Cinegram.Jellyfin;
using Cinegram.MediaFiles;
using Cinegram.TelegramBot;
using Cinegram.Tmdb;
using Cinegram.Transfers;

namespace Cinegram;

/// <summary>
/// Every setting Cinegram takes from its environment, read and checked once at startup. The rest
/// of the code receives the part it needs instead of reading environment variables itself.
/// </summary>
public sealed record CinegramSettings(
    DataPaths Paths,
    LibraryPaths Library,
    AppVersion Version,
    TelegramOptions? Telegram,
    string? TelegramProblem,
    (int ApiId, string ApiHash)? TelegramApi,
    TransferTuning Transfers,
    TmdbSettings Tmdb,
    JellyfinSettings Jellyfin,
    JellyfinBackupOptions? JellyfinBackup)
{
    public static CinegramSettings Read(Func<string, string?> read, string dataRoot = DataPaths.DefaultRoot)
    {
        var (telegram, problem) = TelegramOptions.Read(read);
        var library = LibraryPaths.Read(read);
        return new CinegramSettings(
            DataPaths.Read(read, dataRoot),
            library,
            AppVersion.Read(read),
            telegram,
            problem,
            TelegramOptions.ReadApi(read),
            TransferTuning.Read(read),
            TmdbSettings.Read(read),
            JellyfinSettings.Read(read, library),
            telegram is null ? null : JellyfinBackupOptions.FromEnvironment(read, telegram.OwnerUserId));
    }

    /// <summary>What the process is going to run with, for the startup log. No secrets.</summary>
    public IEnumerable<string> Describe()
    {
        yield return $"[Settings] Version {Version.Current}, data in {Paths.Root}, database {Paths.Database}.";
        yield return $"[Settings] Library: movies {Library.MoviesDir}, shows {Library.ShowsDir}, downloads {Library.DownloadsDir}.";
        yield return Telegram is null
            ? $"[Settings] Telegram: off ({TelegramProblem})."
            : $"[Settings] Telegram: app {Telegram.ApiId}, owner {Telegram.OwnerUserId}, {Telegram.AllowedUserIds.Count} authorized user(s).";
        yield return $"[Settings] Transfers: {Transfers.FilePartSizeBytes / 1024} KB parts, " +
                     $"{Transfers.ParallelTransfersOverride?.ToString() ?? "default"} in flight, " +
                     $"{Transfers.PremiumConnections} account connection(s), split at " +
                     $"{(Transfers.UploadSplitLimitBytes is { } split ? $"{split / 1_000_000} MB" : "the account limit")}.";
        yield return $"[Settings] TMDB: {(Tmdb.ApiKey.Length > 0 ? "key set" : "NO KEY")}, language {Tmdb.Language}.";
        yield return Jellyfin.IsConfigured
            ? $"[Settings] Jellyfin: {Jellyfin.Url}."
            : "[Settings] Jellyfin: off (JELLYFIN_URL or JELLYFIN_TOKEN unset).";
        yield return Jellyfin.PathMappings.Count == 0
            ? "[Settings] Jellyfin paths: no JELLYFIN_PATH_MAP or MEDIA_ROOT, reported paths are used as they are."
            : $"[Settings] Jellyfin paths: {string.Join(", ", Jellyfin.PathMappings.Select(m => $"{m.Prefix} -> {m.Target}"))}.";
        yield return JellyfinBackup is null
            ? "[Settings] Jellyfin backup: off (needs JELLYFIN_BACKUP_DIR and the bot)."
            : $"[Settings] Jellyfin backup: {JellyfinBackup.AppDataDir} every {JellyfinBackup.Interval.TotalHours}h, keeping {JellyfinBackup.Retain}.";
    }
}
