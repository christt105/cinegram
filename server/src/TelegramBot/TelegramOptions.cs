namespace Cinegram.TelegramBot;

/// <summary>
/// What the bot needs to log in. Read once at startup: when something is missing or malformed the
/// bot is left out entirely and the web keeps serving, so <see cref="Read"/> reports why instead of
/// throwing.
/// </summary>
public sealed record TelegramOptions(int ApiId, string ApiHash, string BotToken, long OwnerUserId,
    string BotDatabasePath = TelegramOptions.DefaultBotDatabasePath)
{
    public const string DefaultBotDatabasePath = "/data/bot.sqlite";

    /// <summary>Returns the options, or null with the reason the bot cannot start.</summary>
    public static (TelegramOptions? Options, string? Problem) Read(Func<string, string?> read)
    {
        var missing = new List<string>();

        if (!int.TryParse(read("TELEGRAM_API_ID"), out var apiId)) missing.Add("TELEGRAM_API_ID");
        var apiHash = read("TELEGRAM_API_HASH");
        if (string.IsNullOrWhiteSpace(apiHash)) missing.Add("TELEGRAM_API_HASH");
        var botToken = read("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(botToken)) missing.Add("TELEGRAM_BOT_TOKEN");
        if (!AuthConfig.TryParse(read("TELEGRAM_AUTH_USER_ID"), out var userIds)) missing.Add("TELEGRAM_AUTH_USER_ID");

        return missing.Count > 0
            ? (null, $"{string.Join(", ", missing)} missing or invalid")
            : (new TelegramOptions(apiId, apiHash!, botToken!, userIds[0]), null);
    }
}
