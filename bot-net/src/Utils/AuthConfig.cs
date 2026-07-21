using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Utils;

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
        var raw = Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID") ?? string.Empty;
        var ids = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse)
            .ToList();

        if (ids.Count == 0)
        {
            throw new InvalidOperationException(
                "TELEGRAM_AUTH_USER_ID must contain at least one Telegram user ID.");
        }

        return ids;
    }
}
