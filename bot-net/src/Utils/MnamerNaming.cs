using System.Text.RegularExpressions;

namespace Bot.Utils;

/// <summary>
/// Reproduces the paths mnamer 2.7.2 builds, so a file organised by mnamer and the same
/// file downloaded from Telegram land in the same folder instead of in two look-alike
/// ones. Ports its title casing, padding cleanup and sanitising verbatim, quirks
/// included: the character sets, the partitioning rules and the order the passes run in
/// are the ones in `mnamer/utils.py`, and the formats are the ones mnamer-telegram
/// configures (`{name} ({year}) [tmdbid-{id_tmdb}]`, `{series} [tvdbid-{id_tvdb}]`).
/// </summary>
public static partial class MnamerNaming
{
    private const string PaddingChars = ".- ";
    private const string ParenChars = "[](){}<>";
    private const string PunctuationChars = ParenChars + "\"!?$,-.:;@_`'";
    private const string PartitionChars = PaddingChars + PunctuationChars;

    private static readonly string[] LowercaseExceptions =
    [
        "a", "an", "and", "as", "at", "but", "by", "de", "des", "du", "for", "from",
        "in", "is", "le", "nor", "of", "on", "or", "the", "to", "un", "une", "with", "via"
    ];

    private static readonly string[] UppercaseExceptions =
    [
        "i", "ii", "iii", "iv", "v", "vi", "vii", "viii", "ix", "x", "2d", "3d", "au",
        "aka", "atm", "bbc", "bff", "cia", "csi", "dc", "doa", "espn", "fbi", "ira",
        "jfk", "lol", "mlb", "mlk", "mtv", "nba", "nfl", "nhl", "nsfw", "nyc", "omg",
        "pga", "oj", "rsvp", "tnt", "tv", "ufc", "ufo", "uk", "usa", "vip", "wtf",
        "wwe", "wwi", "wwii", "xxx", "yolo"
    ];

    private static readonly (string Word, string Replacement)[] ReplaceAfter =
        [("&", "and"), ("@", "at"), (";", ",")];

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex EmptyParensPattern();

    [GeneratedRegex(@"\[\s*]")]
    private static partial Regex EmptyBracketsPattern();

    [GeneratedRegex(@"-+")]
    private static partial Regex DashRunPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunPattern();

    [GeneratedRegex(@"( [-.,_])+")]
    private static partial Regex DelimiterRunPattern();

    [GeneratedRegex(@"[<>:""|?*&%=+@#`^]")]
    private static partial Regex IllegalCharsPattern();

    /// <summary>
    /// Builds the folder mnamer files a movie in, e.g.
    /// "Guardianes de La Noche Kimetsu No Yaiba La Fortaleza Infinita (2025) [tmdbid-1311031]".
    /// </summary>
    public static string MovieDirectory(string title, int? year, int? tmdbId)
    {
        var idTag = tmdbId is null ? "" : $" [tmdbid-{tmdbId}]";
        return ProcessComponent($"{TitleCase(title)} ({year}){idTag}");
    }

    /// <summary>
    /// Builds a movie filename. The version tag is cinegram's own addition, appended where
    /// Jellyfin reads it so several downloads of one title can share mnamer's folder.
    /// </summary>
    public static string MovieFile(string title, int? year, string versionTag, string extension) =>
        ProcessComponent($"{TitleCase(title)} ({year}){versionTag}{extension}");

    /// <summary>
    /// Builds the folder mnamer files a show in. Falls back to the TMDB id when the show
    /// has no TVDB one, which mnamer never has to do but cinegram does.
    /// </summary>
    public static string ShowDirectory(string series, int? tvdbId, int? tmdbId)
    {
        var idTag = (tvdbId, tmdbId) switch
        {
            (not null, _) => $" [tvdbid-{tvdbId}]",
            (null, not null) => $" [tmdbid-{tmdbId}]",
            _ => ""
        };
        return ProcessComponent($"{TitleCase(series)}{idTag}");
    }

    /// <summary>Builds the season folder, e.g. "Season 01".</summary>
    public static string SeasonDirectory(int seasonNumber) => $"Season {seasonNumber:D2}";

    /// <summary>Builds an episode filename, e.g. "Kimetsu No Yaiba S01E01.mkv".</summary>
    public static string EpisodeFile(
        string series, int seasonNumber, int episodeNumber, string versionTag, string extension) =>
        ProcessComponent(
            $"{TitleCase(series)} S{seasonNumber:D2}E{episodeNumber:D2}{versionTag}{extension}");

    /// <summary>
    /// Applies mnamer's title casing: everything lowercased, word starts raised, then a
    /// list of small words forced back down and a list of acronyms forced up. A word only
    /// counts when it is delimited on both sides, which is why "de" stays down in
    /// "Guardianes de La Noche" but "Des" would not inside a longer word.
    /// </summary>
    public static string TitleCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        value = value.Replace('/', '-').Replace('\\', '-');

        var length = value.Length;
        var lower = value.ToLowerInvariant();
        var chars = lower.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);

        foreach (var padding in PaddingChars)
        {
            foreach (var pos in FindAll(lower, padding))
            {
                if (pos + 1 == length) break;
                chars[pos + 1] = char.ToUpperInvariant(chars[pos + 1]);
            }
        }

        foreach (var paren in ParenChars)
        {
            foreach (var pos in FindAll(lower, paren))
            {
                if (pos > 0 && !PaddingChars.Contains(lower[pos - 1])) continue;
                if (pos + 1 < length) chars[pos + 1] = char.ToUpperInvariant(chars[pos + 1]);
            }
        }

        foreach (var word in LowercaseExceptions)
        {
            foreach (var pos in FindAll(lower, word))
            {
                if (pos < 2) break;
                if (!PaddingChars.Contains(lower[pos - 1])) continue;

                var ends = pos + word.Length == length;
                if (ends || !PaddingChars.Contains(lower[pos + word.Length])) continue;

                for (var i = 0; i < word.Length; i++) chars[pos + i] = word[i];
            }
        }

        foreach (var word in UppercaseExceptions)
        {
            foreach (var pos in FindAll(lower, word))
            {
                if (pos > 0 && !PartitionChars.Contains(lower[pos - 1])) continue;

                var ends = pos + word.Length == length;
                if (!ends && !PartitionChars.Contains(lower[pos + word.Length])) continue;

                for (var i = 0; i < word.Length; i++) chars[pos + i] = char.ToUpperInvariant(word[i]);
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Collapses the padding a format string leaves behind: empty brackets from a missing
    /// year or id, doubled dashes and repeated delimiters. Runs until the text stops
    /// shrinking, like mnamer's recursive version.
    /// </summary>
    public static string FixPadding(string value)
    {
        while (true)
        {
            var lengthBefore = value.Length;

            value = EmptyParensPattern().Replace(value, "");
            value = EmptyBracketsPattern().Replace(value, "");
            value = DashRunPattern().Replace(value, "-");
            value = WhitespaceRunPattern().Replace(value, " ");
            value = DelimiterRunPattern().Replace(value, "$1");
            value = value.Trim().Trim('-');

            if (value.Length == lengthBefore) return value;
        }
    }

    /// <summary>
    /// Strips the characters mnamer refuses to write to disk. Notably it drops the colon
    /// instead of replacing it, which is why "Guardianes de la noche: Kimetsu" collapses to
    /// "Guardianes de La Noche Kimetsu" — a colon would come back mangled over SMB anyway.
    /// </summary>
    public static string Sanitize(string component)
    {
        var (name, extension) = SplitExtension(component);

        name = WhitespaceRunPattern().Replace(name, " ");
        name = IllegalCharsPattern().Replace(name, "");

        return name.Trim('-', '.', ',', ' ') + extension;
    }

    private static string ProcessComponent(string component) =>
        Sanitize(ApplyReplacements(FixPadding(component)));

    private static string ApplyReplacements(string component)
    {
        var (name, extension) = SplitExtension(component);

        foreach (var (word, replacement) in ReplaceAfter)
        {
            if (name.Contains(word, StringComparison.Ordinal))
                name = Regex.Replace(name, Regex.Escape(word), replacement, RegexOptions.IgnoreCase);
        }

        return name + extension;
    }

    private static (string Name, string Extension) SplitExtension(string component)
    {
        var dot = component.LastIndexOf('.');
        if (dot < 0) return (component, "");

        var firstNonDot = 0;
        while (firstNonDot < dot && component[firstNonDot] == '.') firstNonDot++;

        return firstNonDot == dot
            ? (component, "")
            : (component[..dot], component[dot..]);
    }

    private static IEnumerable<int> FindAll(string value, char needle) =>
        FindAll(value, needle.ToString());

    private static IEnumerable<int> FindAll(string value, string needle)
    {
        var index = value.IndexOf(needle, StringComparison.Ordinal);
        while (index != -1)
        {
            yield return index;
            index = value.IndexOf(needle, index + 1, StringComparison.Ordinal);
        }
    }
}
