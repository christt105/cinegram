using System.Text.RegularExpressions;

namespace Bot.Utils;

/// <summary>
/// Recognises the archive a downloaded file set is packed in and points 7z at the file it
/// has to open. Split volumes are matched by their numbering instead of by a closed list
/// of extensions, so a pack whose dots were sanitised on its way into Telegram
/// ("pack.7z.001" arriving as "pack_7z.001") is still extracted.
/// </summary>
public static partial class ArchiveDetector
{
    private static readonly string[] SingleArchiveExtensions = [".rar", ".zip", ".7z"];

    [GeneratedRegex(@"\.part(\d+)\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex RarVolumePattern();

    [GeneratedRegex(@"\.(\d{2,4})$")]
    private static partial Regex SplitVolumePattern();

    /// <summary>
    /// Picks the file 7z has to be given to unpack a download: the first volume of a split
    /// archive, or a self-contained one. Only first volumes qualify, 7z pulls in the rest
    /// of the set by itself.
    /// </summary>
    /// <param name="paths">Every file that came with the download.</param>
    /// <returns>The path to hand to 7z, or null when nothing looks like an archive.</returns>
    public static string? FindEntry(IEnumerable<string> paths)
    {
        var ordered = paths.OrderBy(path => path, StringComparer.Ordinal).ToList();

        return ordered.FirstOrDefault(IsFirstRarVolume)
            ?? ordered.FirstOrDefault(IsFirstSplitVolume)
            ?? ordered.FirstOrDefault(IsSingleArchive);
    }

    /// <summary>
    /// Tells whether a set of files can yield a video at all: it either carries the video
    /// itself or an archive we know how to open. Lets a download be rejected up front
    /// instead of after pulling gigabytes that end up thrown away.
    /// </summary>
    public static bool CanProduceVideo(IEnumerable<string> paths)
    {
        var all = paths.ToList();
        return all.Any(MediaLibrary.IsVideo) || FindEntry(all) != null;
    }

    private static bool IsFirstRarVolume(string path) => IsFirstVolume(RarVolumePattern(), path);

    private static bool IsFirstSplitVolume(string path) => IsFirstVolume(SplitVolumePattern(), path);

    private static bool IsFirstVolume(Regex pattern, string path)
    {
        var match = pattern.Match(path);
        return match.Success && int.Parse(match.Groups[1].Value) == 1;
    }

    private static bool IsSingleArchive(string path) =>
        SingleArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
