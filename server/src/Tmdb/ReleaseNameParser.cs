using System.Text.RegularExpressions;
using Cinegram.Data;

namespace Cinegram.Tmdb;

/// <summary>What a release filename says about its title: the search query left after stripping
/// release noise, an embedded TMDB id, the release year and any season/episode markers.</summary>
public sealed record ParsedReleaseName(
    int? TmdbId, string CleanName, string Type, int? Season, int? Episode, int? Year);

/// <summary>
/// Turns a download's filename into a TMDB search query. Season and episode always come from the
/// filename itself, never from TMDB, which only resolves the movie or series identity.
/// </summary>
public static partial class ReleaseNameParser
{
    private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex[] NoisePatterns = new[]
    {
        @"\b[a-z]?\d{3,4}p\b",
        @"\b(?:Micro)?4K\b",
        @"\bHDR10\+?\b",
        @"\bHDR\b",
        @"\b(?:8|10|12)-?[Bb]it\b",
        @"\bBlu[- ]?ray\b",
        @"\bHEVC\b",
        @"\bWEB[- ]?DL\b",
        @"\b(?:BD|BR|DVD|HD|WEB)Rip\b",
        @"\b(?:BD|BR|DVD)?Remux\b",
        @"\.part\d+",
        @"\.\d+$",
        @"\[.*?\]",
        @"\(.*?\)",
        @"\bE?AC3\b",
        @"\bDTS\b",
        @"\bFLAC\b",
        @"\bXviD\b",
        @"\bx26[45]\b",
        @"\bH\.?26[45]\b",
        @"\bAAC\b",
        @"\bMP3\b",
        @"\bExt\.?\b",
        @"\bLas\s*Cositas\b",
    }.Select(p => new Regex(p, IgnoreCase)).ToArray();

    [GeneratedRegex(@"\[?tmdbid[-_](\d+)\]?", IgnoreCase)]
    private static partial Regex TmdbIdTag();

    [GeneratedRegex(@"(?<![^\W_])(19\d{2}|20\d{2})(?![^\W_])")]
    private static partial Regex YearToken();

    [GeneratedRegex(@"[\s.\-_(\[]+$")]
    private static partial Regex TrailingSeparators();

    [GeneratedRegex(@"\bby[\s._-]+[A-Za-z0-9_]{3,}[\s._-]*$", IgnoreCase)]
    private static partial Regex UploaderCredit();

    [GeneratedRegex(@"-[A-Za-z0-9]+$")]
    private static partial Regex ReleaseGroupTag();

    [GeneratedRegex(@"\.(zip|7z|rar|mkv|avi|mp4)$", IgnoreCase)]
    private static partial Regex StackedExtension();

    [GeneratedRegex(@"\.(mkv|avi|mp4)$", IgnoreCase)]
    private static partial Regex VideoExtension();

    [GeneratedRegex(@"[Ss](\d{1,2})[Ee](\d{1,3})")]
    private static partial Regex SeasonEpisode();

    [GeneratedRegex(@"(?<!\d)(\d{1,2})x(\d{1,3})(?!\d)", IgnoreCase)]
    private static partial Regex CrossEpisode();

    [GeneratedRegex(@"(?<![a-zA-Z0-9])[Ss](\d{1,2})\s*[-_]\s*(\d{1,3})(?!\d)")]
    private static partial Regex SeasonDashEpisode();

    [GeneratedRegex(@"(?<![a-zA-Z0-9])[Ss](\d{1,2})\s*[-_]\s*(\d{1,3})(?!\d)", IgnoreCase)]
    private static partial Regex SeasonDashEpisodeAnyCase();

    [GeneratedRegex(@"\s+[-_]\s+(\d{1,3})(?!\d)$")]
    private static partial Regex TrailingDashEpisode();

    [GeneratedRegex(@"(?<![a-zA-Z0-9])(?:Temporada|Season)\s+(\d+)(?![a-zA-Z0-9])", IgnoreCase)]
    private static partial Regex SeasonWord();

    [GeneratedRegex(@"[A-Za-z]\s+\d{4,}$")]
    private static partial Regex AbsoluteEpisodeNumber();

    [GeneratedRegex(
        @"[Ss]\d{1,2}[Ee]\d{1,3}|(?<!\d)\d{1,2}x\d{1,3}(?!\d)|(?<![a-zA-Z0-9])[Ss]\d{1,2}\s*[-_]\s*\d{1,3}(?!\d)|\s+[-_]\s+\d{1,3}(?!\d)$",
        IgnoreCase)]
    private static partial Regex EpisodeMarker();

    [GeneratedRegex(@" - (?:Temporada|Season)\s+\d+.*$", IgnoreCase)]
    private static partial Regex SeasonTail();

    [GeneratedRegex(@"[•·]+")]
    private static partial Regex Bullets();

    [GeneratedRegex(@"\.+")]
    private static partial Regex Dots();

    [GeneratedRegex(@"_+")]
    private static partial Regex Underscores();

    [GeneratedRegex(@"(?<![\w])-+|-+(?![\w])")]
    private static partial Regex LooseDashes();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static ParsedReleaseName Parse(string filename)
    {
        var name = StripExtension(filename);

        int? tmdbId = null;
        var tagMatch = TmdbIdTag().Match(name);
        if (tagMatch.Success)
        {
            tmdbId = int.Parse(tagMatch.Groups[1].Value);
            name = TmdbIdTag().Replace(name, "");
        }

        int? year = null;
        string? yearPrefix = null;
        var yearMatches = YearToken().Matches(name);
        if (yearMatches.Count > 0)
        {
            var yearMatch = yearMatches[^1];
            var prefix = name[..yearMatch.Index];
            var suffix = name[(yearMatch.Index + yearMatch.Length)..];
            var hasPrefix = TrailingSeparators().Replace(prefix, "").Trim().Length > 0;
            var hasSuffix = suffix.Trim(' ', '.', '-', '_', ')', ']').Length > 0;
            if (hasPrefix || hasSuffix)
            {
                year = int.Parse(yearMatch.Groups[1].Value);
                yearPrefix = prefix;
                name = prefix + suffix;
            }
        }

        foreach (var pattern in NoisePatterns)
            name = pattern.Replace(name, "");

        name = name.Trim();
        if (name.Contains('.'))
            name = UploaderCredit().Replace(name, "");

        name = name.Trim();
        if (name.Contains('.'))
            name = ReleaseGroupTag().Replace(name, "");

        name = StackedExtension().Replace(name, "").Trim();

        var (season, episode) = ParseSeasonEpisode(name);
        var type = DetectType(name);

        if (type == "movie" && yearPrefix is not null)
        {
            var truncated = TrailingSeparators().Replace(yearPrefix, "");
            if (truncated.Trim().Length > 0) name = truncated;
        }

        if (type == "tv")
        {
            var parts = EpisodeMarker().Split(name);
            var before = parts[0].Trim(' ', '-', '_');
            if (before.Length > 0)
            {
                name = before;
            }
            else if (parts.Length > 1)
            {
                var after = parts[1].Trim(' ', '-', '_');
                name = after.Contains(" - ") ? after.Split(" - ")[0].Trim() : after;
            }

            var tail = SeasonTail().Match(name);
            if (tail.Success) name = name[..tail.Index];
        }

        name = VideoExtension().Replace(name, "");
        name = Bullets().Replace(name, " ");
        name = Dots().Replace(name, " ");
        name = Underscores().Replace(name, " ");
        name = LooseDashes().Replace(name, " ");
        name = Whitespace().Replace(name, " ").Trim();

        return new ParsedReleaseName(tmdbId, name, type, season, episode, year);
    }

    private static (int? Season, int? Episode) ParseSeasonEpisode(string name)
    {
        var match = SeasonEpisode().Match(name);
        if (match.Success) return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

        match = CrossEpisode().Match(name);
        if (match.Success) return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

        match = SeasonDashEpisode().Match(name);
        if (match.Success) return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

        match = TrailingDashEpisode().Match(name);
        if (match.Success) return (1, int.Parse(match.Groups[1].Value));

        match = SeasonWord().Match(name);
        if (match.Success) return (int.Parse(match.Groups[1].Value), null);

        return (null, null);
    }

    /// <summary>A trailing number needs four or more digits to count as an absolute episode
    /// ("One Piece 1085"), so titles ending in a smaller number ("Fahrenheit 451") stay movies.</summary>
    private static string DetectType(string name) =>
        SeasonEpisode().IsMatch(name)
        || CrossEpisode().IsMatch(name)
        || SeasonDashEpisodeAnyCase().IsMatch(name)
        || TrailingDashEpisode().IsMatch(name)
        || SeasonWord().IsMatch(name)
        || AbsoluteEpisodeNumber().IsMatch(name)
            ? "tv"
            : "movie";

    /// <summary>Drops the last extension the way Python's <c>os.path.splitext</c> does: a name
    /// made only of leading dots before the extension has none.</summary>
    private static string StripExtension(string filename)
    {
        var dot = filename.LastIndexOf('.');
        if (dot <= 0) return filename;

        var stem = filename[..dot];
        return stem.All(c => c == '.') ? filename : stem;
    }
}
