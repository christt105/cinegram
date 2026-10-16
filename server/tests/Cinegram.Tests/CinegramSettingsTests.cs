using Xunit;
using Cinegram.TelegramBot;

namespace Cinegram.Tests;

public class CinegramSettingsTests
{
    private static CinegramSettings Read(Dictionary<string, string?> values) =>
        CinegramSettings.Read(name => values.GetValueOrDefault(name));

    [Fact]
    public void With_nothing_set_every_path_and_default_is_the_one_the_image_has_always_used()
    {
        var settings = Read(new());

        Assert.Equal("/data/db/database.db", settings.Paths.Database);
        Assert.Equal("/data/bot.sqlite", settings.Paths.BotDatabase);
        Assert.Equal("/data/user_client.session", settings.Paths.UserSession);
        Assert.Equal("/data/temp/downloads", settings.Paths.TempDownloads);
        Assert.Equal("/data/temp/uploads", settings.Paths.TempUploads);
        Assert.Equal("/data/temp/backups", settings.Paths.TempBackups);
        Assert.Equal("/data/jellyfin-backup-last-run", settings.Paths.JellyfinBackupLastRun);
        Assert.Equal("/data/jellyfin-backup-history.json", settings.Paths.JellyfinBackupHistory);
        Assert.Equal("dev", settings.Version.Current);

        Assert.Null(settings.Telegram);
        Assert.Null(settings.TelegramApi);
        Assert.NotNull(settings.TelegramProblem);

        Assert.Null(settings.Transfers.ParallelTransfersOverride);
        Assert.Equal(4, settings.Transfers.ParallelTransfers(4));
        Assert.Equal(512 * 1024, settings.Transfers.FilePartSizeBytes);
        Assert.Equal(3, settings.Transfers.PremiumConnections);
        Assert.Equal(1_950_000_000L, settings.Transfers.SplitLimitBytes(premiumAccount: false));
        Assert.Equal(3_900_000_000L, settings.Transfers.SplitLimitBytes(premiumAccount: true));

        Assert.Equal("", settings.Tmdb.ApiKey);
        Assert.Equal("en-US", settings.Tmdb.Language);
        Assert.False(settings.Jellyfin.IsConfigured);
        Assert.Null(settings.JellyfinBackup);
    }

    [Fact]
    public void Every_variable_lands_in_its_setting()
    {
        var settings = Read(new()
        {
            ["DATABASE_PATH"] = "/elsewhere/cinegram.db",
            ["APP_VERSION"] = "v9.9.9",
            ["TELEGRAM_API_ID"] = "12345",
            ["TELEGRAM_API_HASH"] = "hash",
            ["TELEGRAM_BOT_TOKEN"] = "123:token",
            ["TELEGRAM_AUTH_USER_ID"] = "111,222",
            ["TELEGRAM_PARALLEL_TRANSFERS"] = "6",
            ["TELEGRAM_FILE_PART_SIZE_KB"] = "256",
            ["PREMIUM_DOWNLOAD_CONNECTIONS"] = "5",
            ["UPLOAD_SPLIT_LIMIT_MB"] = "1500",
            ["TMDB_API_KEY"] = "tmdb-key",
            ["TMDB_CONTENT_LANGUAGE"] = "es-ES",
            ["JELLYFIN_URL"] = "http://jellyfin:8096",
            ["JELLYFIN_TOKEN"] = "jellyfin-token",
            ["JELLYFIN_BACKUP_DIR"] = "/jellyfin",
            ["JELLYFIN_BACKUP_INTERVAL_HOURS"] = "24",
            ["JELLYFIN_BACKUP_RETAIN"] = "2",
        });

        Assert.Equal("/elsewhere/cinegram.db", settings.Paths.Database);
        Assert.Equal("v9.9.9", settings.Version.Current);
        Assert.Equal(new TelegramOptions(12345, "hash", "123:token", 111), settings.Telegram);
        Assert.Equal((12345, "hash"), settings.TelegramApi);
        Assert.Equal(6, settings.Transfers.ParallelTransfers(4));
        Assert.Equal(256 * 1024, settings.Transfers.FilePartSizeBytes);
        Assert.Equal(5, settings.Transfers.PremiumConnections);
        Assert.Equal(1_500_000_000L, settings.Transfers.SplitLimitBytes(premiumAccount: true));
        Assert.Equal("tmdb-key", settings.Tmdb.ApiKey);
        Assert.Equal("es-ES", settings.Tmdb.Language);
        Assert.Equal("http://jellyfin:8096", settings.Jellyfin.Url);
        Assert.Equal("jellyfin-token", settings.Jellyfin.Token);
        Assert.Equal("/jellyfin", settings.JellyfinBackup!.AppDataDir);
        Assert.Equal(TimeSpan.FromHours(24), settings.JellyfinBackup.Interval);
        Assert.Equal(111, settings.JellyfinBackup.ChatId);
        Assert.Equal(2, settings.JellyfinBackup.Retain);
    }

    [Fact]
    public void Malformed_numbers_fall_back_to_the_defaults()
    {
        var settings = Read(new()
        {
            ["TELEGRAM_PARALLEL_TRANSFERS"] = "lots",
            ["TELEGRAM_FILE_PART_SIZE_KB"] = "-1",
            ["PREMIUM_DOWNLOAD_CONNECTIONS"] = "many",
            ["UPLOAD_SPLIT_LIMIT_MB"] = "big",
        });

        Assert.Null(settings.Transfers.ParallelTransfersOverride);
        Assert.Equal(512 * 1024, settings.Transfers.FilePartSizeBytes);
        Assert.Equal(3, settings.Transfers.PremiumConnections);
        Assert.Null(settings.Transfers.UploadSplitLimitBytes);
    }

    [Fact]
    public void The_file_part_size_never_goes_above_the_protocol_maximum()
    {
        Assert.Equal(512 * 1024, Read(new() { ["TELEGRAM_FILE_PART_SIZE_KB"] = "1024" }).Transfers.FilePartSizeBytes);
    }

    [Fact]
    public void Jellyfin_backups_stay_off_without_the_bot_that_sends_them()
    {
        Assert.Null(Read(new() { ["JELLYFIN_BACKUP_DIR"] = "/jellyfin" }).JellyfinBackup);
    }

    [Fact]
    public void The_startup_description_never_shows_a_secret()
    {
        var lines = string.Join("\n", Read(new()
        {
            ["TELEGRAM_API_ID"] = "12345",
            ["TELEGRAM_API_HASH"] = "secret-hash",
            ["TELEGRAM_BOT_TOKEN"] = "123:secret-token",
            ["TELEGRAM_AUTH_USER_ID"] = "111",
            ["TMDB_API_KEY"] = "secret-tmdb",
            ["JELLYFIN_URL"] = "http://jellyfin:8096",
            ["JELLYFIN_TOKEN"] = "secret-jellyfin",
        }).Describe());

        Assert.DoesNotContain("secret", lines);
        Assert.Contains("http://jellyfin:8096", lines);
    }
}
