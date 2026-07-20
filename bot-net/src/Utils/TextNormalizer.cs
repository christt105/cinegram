using System.Globalization;
using System.Text;

namespace Bot.Utils;

public static class TextNormalizer
{
    /// <summary>
    /// Normalizes text for accent-insensitive comparisons: strips diacritics
    /// (via FormD decomposition, dropping non-spacing marks), maps special
    /// separators (·, –, —, -) to spaces and collapses whitespace, lowercasing
    /// the result. So "Pokémon", "WALL·E" and "Wall-E" become comparable to
    /// "pokemon", "wall e" and "wall e".
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c is '·' or '–' or '—' or '-' ? ' ' : c);
        }

        var collapsed = sb.ToString().Normalize(NormalizationForm.FormC);
        collapsed = string.Join(' ', collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToLowerInvariant();
    }
}
