using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cinegram.Services;

namespace Cinegram.Utils;

/// <summary>
/// Picks the TMDB result a cleaned filename most likely refers to. Similarity is compared against
/// both the localized and the original title, since scene releases use the original one, which
/// TMDB_CONTENT_LANGUAGE can localize away ("The Wire" becomes "Bajo escucha" in es-ES).
/// </summary>
public static partial class TitleMatcher
{
    public const double MinSimilarity = 0.4;

    /// <summary>
    /// Weight of <c>log10(popularity + 1)</c> in the ranking. Large enough for an orders-of-magnitude
    /// gap to break a near tie ("Parasites", popularity ~1, against "Parásitos", ~40), small enough
    /// that ordinary gaps between sequels can't flip which one a numbered title resolves to.
    /// </summary>
    public const double PopularityWeight = 0.15;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Strips diacritics and maps separators (·, •, –, —, -) to spaces, so "Pokémon" and
    /// "Pokemon", or "WALL·E" and "Wall-E", compare equal.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var decomposed = text.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c is '·' or '•' or '–' or '—' or '-' ? ' ' : c);
        }

        return Whitespace().Replace(sb.ToString(), " ").Trim().ToLowerInvariant();
    }

    /// <summary>The best candidate with at least <see cref="MinSimilarity"/>, carrying its
    /// similarity as <see cref="TmdbTitle.MatchScore"/>; null when none qualifies.</summary>
    public static TmdbTitle? BestMatch(IReadOnlyList<TmdbTitle> results, string cleanName)
    {
        var query = Normalize(cleanName);
        TmdbTitle? best = null;
        var bestSimilarity = 0.0;
        var bestRank = double.NegativeInfinity;

        foreach (var result in results)
        {
            var similarity = Similarity(query, Normalize(result.Title ?? result.Name));
            var original = Normalize(result.OriginalTitle ?? result.OriginalName);
            if (original.Length > 0) similarity = Math.Max(similarity, Similarity(query, original));
            if (similarity < MinSimilarity) continue;

            var rank = similarity + PopularityWeight * Math.Log10(result.Popularity + 1);
            if (rank > bestRank)
            {
                best = result;
                bestSimilarity = similarity;
                bestRank = rank;
            }
        }

        return best is null ? null : best with { MatchScore = bestSimilarity };
    }

    /// <summary>Python's <c>difflib.SequenceMatcher(None, a, b).ratio()</c>, code point by code
    /// point, including its autojunk heuristic for sequences of 200 or more.</summary>
    public static double Similarity(string a, string b)
    {
        var left = CodePoints(a);
        var right = CodePoints(b);
        var total = left.Length + right.Length;
        if (total == 0) return 1.0;

        return 2.0 * MatchingCharacters(left, right) / total;
    }

    private static int[] CodePoints(string text) => text.EnumerateRunes().Select(r => r.Value).ToArray();

    private static int MatchingCharacters(int[] a, int[] b)
    {
        var b2j = new Dictionary<int, List<int>>();
        for (var j = 0; j < b.Length; j++)
        {
            if (!b2j.TryGetValue(b[j], out var indices)) b2j[b[j]] = indices = [];
            indices.Add(j);
        }

        if (b.Length >= 200)
        {
            var popular = b.Length / 100 + 1;
            foreach (var key in b2j.Where(kv => kv.Value.Count > popular).Select(kv => kv.Key).ToList())
                b2j.Remove(key);
        }

        var matched = 0;
        var queue = new Stack<(int ALo, int AHi, int BLo, int BHi)>();
        queue.Push((0, a.Length, 0, b.Length));
        while (queue.Count > 0)
        {
            var (alo, ahi, blo, bhi) = queue.Pop();
            var (i, j, k) = LongestMatch(a, b, b2j, alo, ahi, blo, bhi);
            if (k == 0) continue;

            matched += k;
            if (alo < i && blo < j) queue.Push((alo, i, blo, j));
            if (i + k < ahi && j + k < bhi) queue.Push((i + k, ahi, j + k, bhi));
        }

        return matched;
    }

    private static (int I, int J, int Size) LongestMatch(
        int[] a, int[] b, Dictionary<int, List<int>> b2j, int alo, int ahi, int blo, int bhi)
    {
        var (besti, bestj, bestSize) = (alo, blo, 0);
        var j2len = new Dictionary<int, int>();

        for (var i = alo; i < ahi; i++)
        {
            var next = new Dictionary<int, int>();
            if (b2j.TryGetValue(a[i], out var indices))
            {
                foreach (var j in indices)
                {
                    if (j < blo) continue;
                    if (j >= bhi) break;

                    var k = next[j] = j2len.GetValueOrDefault(j - 1) + 1;
                    if (k > bestSize) (besti, bestj, bestSize) = (i - k + 1, j - k + 1, k);
                }
            }
            j2len = next;
        }

        while (besti > alo && bestj > blo && a[besti - 1] == b[bestj - 1])
            (besti, bestj, bestSize) = (besti - 1, bestj - 1, bestSize + 1);

        while (besti + bestSize < ahi && bestj + bestSize < bhi && a[besti + bestSize] == b[bestj + bestSize])
            bestSize++;

        return (besti, bestj, bestSize);
    }
}
