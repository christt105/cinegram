namespace Bot.Utils;

/// <summary>
/// Builds the final on-disk names for downloaded media.
/// </summary>
public static class MediaNaming
{
    private const int MaxSuffixLength = 40;

    /// <summary>
    /// Builds the " - [tag]" part of a filename out of the quality and an optional
    /// version suffix. Jellyfin reads whatever follows the dash as the version name, so
    /// two collections of the same movie can share a folder and show up as alternate
    /// versions instead of overwriting each other.
    /// </summary>
    /// <param name="quality">Quality label of the collection, e.g. "1080p".</param>
    /// <param name="nameSuffix">Version suffix chosen when queueing the download.</param>
    /// <returns>The tag to append before the extension, empty if there is nothing to add.</returns>
    public static string BuildVersionTag(string? quality, string? nameSuffix)
    {
        var parts = new[] { quality?.Trim(), SanitizeSuffix(nameSuffix) }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var tag = string.Join(" ", parts);
        return tag.Length == 0 ? "" : $" - [{tag}]";
    }

    /// <summary>
    /// Strips path-hostile characters and brackets from a user provided suffix, collapses
    /// whitespace and caps its length.
    /// </summary>
    public static string SanitizeSuffix(string? nameSuffix)
    {
        if (string.IsNullOrWhiteSpace(nameSuffix)) return "";

        var invalid = Path.GetInvalidFileNameChars().Concat(['[', ']']).ToArray();
        var cleaned = new string(nameSuffix.Where(c => !invalid.Contains(c)).ToArray());
        cleaned = string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return cleaned.Length > MaxSuffixLength ? cleaned[..MaxSuffixLength].TrimEnd() : cleaned;
    }

    /// <summary>
    /// Picks a destination that will not clobber somebody else's file: the desired path is
    /// reused only when it is free or when it already belongs to this collection,
    /// otherwise a numbered variant is returned.
    /// </summary>
    /// <param name="desiredPath">Path the naming convention asks for.</param>
    /// <param name="ownedPath">Path this collection wrote on a previous download, if any.</param>
    /// <param name="exists">Existence check, injected so it can be exercised in isolation.</param>
    /// <exception cref="Exception">Thrown when no free variant is found.</exception>
    public static string ResolveFreePath(string desiredPath, string? ownedPath, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;

        if (!exists(desiredPath) || IsSamePath(desiredPath, ownedPath))
            return desiredPath;

        var dir = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        for (var i = 2; i < 100; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){extension}");
            if (!exists(candidate) || IsSamePath(candidate, ownedPath))
                return candidate;
        }

        throw new Exception($"Could not find a free filename for {desiredPath}.");
    }

    private static bool IsSamePath(string path, string? other) =>
        other != null && string.Equals(path, other, StringComparison.Ordinal);
}
