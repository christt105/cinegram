using System.Globalization;

namespace Cinegram.Infrastructure;

/// <summary>
/// Parsing helpers for the settings records. A value that is set but cannot be used is reported
/// once at startup and treated as unset, so the default applies as it always did.
/// </summary>
public static class Settings
{
    public static string? Text(Func<string, string?> read, string name) =>
        read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    public static int? PositiveInt(Func<string, string?> read, string name)
    {
        var raw = Text(read, name);
        if (raw is null) return null;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0) return value;

        Log.Warning($"[Settings] {name} is not a positive number ('{raw}'), using the default.");
        return null;
    }

    public static long? Long(Func<string, string?> read, string name)
    {
        var raw = Text(read, name);
        if (raw is null) return null;
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;

        Log.Warning($"[Settings] {name} is not a number ('{raw}'), using the default.");
        return null;
    }
}
