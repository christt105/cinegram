using Cinegram.Infrastructure;
using Cinegram.MediaFiles;

namespace Cinegram.Jellyfin;

/// <summary>
/// Where Jellyfin's API is, the token for it, and how the paths Jellyfin reports map onto this
/// container. URL and token are optional: without them the web gets no Jellyfin proxy, confirmed
/// series are identified by the folder tag alone and the startup library path check is skipped.
/// </summary>
public sealed record JellyfinSettings(string? Url, string? Token, IReadOnlyList<PathMapping> PathMappings)
{
    public static JellyfinSettings Read(Func<string, string?> read, LibraryPaths library) => new(
        Settings.Text(read, "JELLYFIN_URL"),
        Settings.Text(read, "JELLYFIN_TOKEN"),
        PathTranslator.ConfiguredMappings(
            read("JELLYFIN_PATH_MAP"), read("MEDIA_ROOT"), read("MOVIES_SUBDIR"), read("SHOWS_SUBDIR"), library).ToList());

    public bool IsConfigured => Url is not null && Token is not null;
}
