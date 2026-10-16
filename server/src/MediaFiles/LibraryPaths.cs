namespace Cinegram.MediaFiles;

/// <summary>
/// The mounted media directories: the movie and show libraries Jellyfin serves, and the downloads
/// folder whose new files are identified and moved into them.
/// </summary>
public sealed record LibraryPaths(string MoviesDir, string ShowsDir, string DownloadsDir)
{
    public static LibraryPaths Read(Func<string, string?> read) => new(
        read("JELLYFIN_MOVIES_DIR") ?? "/data/jellyfin/movies",
        read("JELLYFIN_SHOWS_DIR") ?? "/data/jellyfin/shows",
        read("DOWNLOADS_DIR") ?? "/data/media/downloads");

    public string[] Roots() => [MoviesDir, ShowsDir];

    /// <summary>
    /// Resolves a path and confirms it lives inside one of the library roots. Callers use
    /// it to make sure an HTTP request cannot reach files outside the mounted directories.
    /// </summary>
    /// <param name="path">Path to check, absolute or relative.</param>
    /// <param name="fullPath">The resolved absolute path.</param>
    /// <returns>True when the resolved path is inside a library root.</returns>
    public bool TryResolveInsideLibrary(string path, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(path)) return false;

        fullPath = Path.GetFullPath(path);

        foreach (var root in Roots())
        {
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot += Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
