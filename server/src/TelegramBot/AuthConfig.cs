using System;
using System.Collections.Generic;
using System.Linq;

namespace Cinegram.TelegramBot;

/// <summary>
/// Resolves the set of Telegram users allowed to command the bot from the
/// <c>TELEGRAM_AUTH_USER_ID</c> environment variable, which may hold a single
/// ID or a comma-separated list.
/// </summary>
public static class AuthConfig
{
    /// <summary>
    /// All Telegram user IDs authorized to command the bot. The first entry is
    /// the owner (see <see cref="OwnerUserId"/>).
    /// </summary>
    public static IReadOnlyList<long> AllowedUserIds { get; } = Parse();

    /// <summary>
    /// The owner user ID. Media files live in this user's chat, so every file
    /// transfer, preview and startup message targets this single peer even when
    /// several users are authorized.
    /// </summary>
    public static long OwnerUserId => AllowedUserIds[0];

    /// <summary>Returns whether the given Telegram user ID is authorized.</summary>
    public static bool IsAllowed(long userId) => AllowedUserIds.Contains(userId);

    private static IReadOnlyList<long> Parse()
    {
        if (!TryParse(Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID"), out var ids))
        {
            throw new InvalidOperationException(
                "TELEGRAM_AUTH_USER_ID must contain at least one Telegram user ID.");
        }

        return ids;
    }

    /// <summary>Parses a single ID or a comma-separated list; false when empty or malformed.</summary>
    public static bool TryParse(string? raw, out IReadOnlyList<long> ids)
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
