using System.Text.RegularExpressions;

namespace Bot.Utils;

/// <summary>
/// Reads back what a library path encodes: the ids Jellyfin folders carry and the version
/// tag and resolution <see cref="MediaNaming"/> writes into filenames. Used to tell apart
/// the copies of a title already sitting on disk.
/// </summary>
public static partial class MediaNameParser
{
    private static readonly string[] IdTagPrefixes = ["tmdbid-", "tvdbid-", "imdbid-"];

    [GeneratedRegex(@"\[tmdbid-(\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex TmdbIdPattern();

    [GeneratedRegex(@"\[tvdbid-(\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex TvdbIdPattern();

    [GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(?<![a-z0-9])(\d{3,4}p|4k|8k)(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPattern();

    /// <summary>
    /// Reads the TMDB id out of a path, e.g. "Movie (2025) [tmdbid-42]/Movie (2025).mkv".
    /// The deepest id wins, so a file inherits the id of its own folder.
    /// </summary>
    public static int? ParseTmdbId(string path) => ParseId(TmdbIdPattern(), path);

    /// <summary>
    /// Reads the TVDB id out of a path, e.g. "Show [tvdbid-42]/Season 01/Show - S01E01.mkv".
    /// </summary>
    public static int? ParseTvdbId(string path) => ParseId(TvdbIdPattern(), path);

    /// <summary>
    /// Tells whether a path belongs to one of the given ids. A null id does not filter, and
    /// when both are given either match is enough: a show can be filed under its TVDB id
    /// while the collection asking only knows its TMDB one.
    /// </summary>
    public static bool MatchesIds(string path, int? tmdbId, int? tvdbId)
    {
        if (tmdbId is null && tvdbId is null) return true;

        return (tmdbId is not null && ParseTmdbId(path) == tmdbId)
            || (tvdbId is not null && ParseTvdbId(path) == tvdbId);
    }

    /// <summary>
    /// Returns the version tag of a filename, the last "[...]" group written by
    /// <see cref="MediaNaming.BuildVersionTag"/>, e.g. "1080p Erai BDRip". Id brackets are
    /// not versions, so they are skipped.
    /// </summary>
    public static string? ParseVersionTag(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        return TagPattern().Matches(name)
            .Select(match => match.Groups[1].Value.Trim())
            .LastOrDefault(tag => tag.Length > 0 && !IsIdTag(tag));
    }

    /// <summary>
    /// Returns the resolution written in a filename ("1080p", "4K"), or null when it says
    /// nothing about it.
    /// </summary>
    public static string? ParseQuality(string path)
    {
        var match = QualityPattern().Match(Path.GetFileName(path));
        if (!match.Success) return null;

        var quality = match.Groups[1].Value;
        return quality.EndsWith('p') || quality.EndsWith('P')
            ? quality.ToLowerInvariant()
            : quality.ToUpperInvariant();
    }

    private static int? ParseId(Regex pattern, string path)
    {
        var matches = pattern.Matches(path);
        return matches.Count == 0 ? null : int.Parse(matches[^1].Groups[1].Value);
    }

    private static bool IsIdTag(string tag) =>
        IdTagPrefixes.Any(prefix => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
