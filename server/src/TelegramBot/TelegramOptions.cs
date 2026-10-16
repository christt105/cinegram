namespace Cinegram.TelegramBot;

/// <summary>
/// What the bot needs to log in. Read once at startup: when something is missing or malformed the
/// bot is left out entirely and the web keeps serving, so <see cref="Read"/> reports why instead of
/// throwing.
/// </summary>
public sealed record TelegramOptions(int ApiId, string ApiHash, string BotToken, IReadOnlyList<long> AllowedUserIds)
{
    /// <summary>
    /// The first authorized user. Media files live in this user's chat, so every file transfer,
    /// preview and startup message targets this single peer even when several users are authorized.
    /// </summary>
    public long OwnerUserId => AllowedUserIds[0];

    /// <summary>Whether the given Telegram user may command the bot.</summary>
    public bool IsAllowed(long userId) => AllowedUserIds.Contains(userId);

    /// <summary>
    /// Just the app credentials, which is all the console login of the user account needs.
    /// Null when either is missing.
    /// </summary>
    public static (int ApiId, string ApiHash)? ReadApi(Func<string, string?> read) =>
        int.TryParse(read("TELEGRAM_API_ID"), out var apiId) && !string.IsNullOrWhiteSpace(read("TELEGRAM_API_HASH"))
            ? (apiId, read("TELEGRAM_API_HASH")!)
            : null;

    /// <summary>Returns the options, or null with the reason the bot cannot start.</summary>
    public static (TelegramOptions? Options, string? Problem) Read(Func<string, string?> read)
    {
        var missing = new List<string>();

        if (!int.TryParse(read("TELEGRAM_API_ID"), out var apiId)) missing.Add("TELEGRAM_API_ID");
        var apiHash = read("TELEGRAM_API_HASH");
        if (string.IsNullOrWhiteSpace(apiHash)) missing.Add("TELEGRAM_API_HASH");
        var botToken = read("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(botToken)) missing.Add("TELEGRAM_BOT_TOKEN");
        if (!TryParseUserIds(read("TELEGRAM_AUTH_USER_ID"), out var userIds)) missing.Add("TELEGRAM_AUTH_USER_ID");

        return missing.Count > 0
            ? (null, $"{string.Join(", ", missing)} missing or invalid")
            : (new TelegramOptions(apiId, apiHash!, botToken!, userIds), null);
    }

    /// <summary>A single ID or a comma-separated list; false when empty or malformed.</summary>
    private static bool TryParseUserIds(string? raw, out IReadOnlyList<long> ids)
    {
        var parsed = new List<long>();
        foreach (var part in (raw ?? string.Empty).Split(',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(part, out var id))
            {
                ids = [];
                return false;
            }
            parsed.Add(id);
        }

        ids = parsed;
        return parsed.Count > 0;
    }
}
