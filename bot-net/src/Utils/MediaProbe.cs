using System.Diagnostics;
using System.Text.Json;

namespace Bot.Utils;

/// <summary>
/// Reads technical metadata from media files with ffprobe.
/// </summary>
public static class MediaProbe
{
    public record struct Summary(string? AudioLanguages, string? SubtitleLanguages);

    /// <summary>
    /// Pulls a collection-ready summary out of ffprobe's raw JSON: the distinct language
    /// tags of the audio and subtitle streams. Null fields mean the file said nothing
    /// usable for that piece (no streams of that type, or none with a "language"/
    /// "LANGUAGE" tag). Resolution is deliberately not extracted here: the backend already
    /// derives "quality" from technical_metadata itself on patch (with HDR/2K handling
    /// this would only duplicate worse), as long as the request doesn't also set "quality".
    /// </summary>
    public static Summary Summarize(string ffprobeJson)
    {
        using var doc = JsonDocument.Parse(ffprobeJson);
        var streams = doc.RootElement.TryGetProperty("streams", out var s) ? s : default;

        return new Summary(
            JoinLanguages(streams, "audio"),
            JoinLanguages(streams, "subtitle"));
    }

    private static string? JoinLanguages(JsonElement streams, string codecType)
    {
        if (streams.ValueKind != JsonValueKind.Array) return null;

        var languages = streams.EnumerateArray()
            .Where(stream => GetString(stream, "codec_type") == codecType)
            .Select(stream => stream.TryGetProperty("tags", out var tags)
                ? GetString(tags, "language") ?? GetString(tags, "LANGUAGE")
                : null)
            .Where(lang => !string.IsNullOrWhiteSpace(lang))
            .Select(lang => lang!)
            .Distinct()
            .ToList();

        return languages.Count == 0 ? null : string.Join(",", languages);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Runs ffprobe on a file and returns its raw JSON report (format + streams),
    /// which is what the backend stores as the collection's technical metadata.
    /// </summary>
    /// <param name="filePath">Path of the media file to inspect.</param>
    /// <returns>The ffprobe JSON output.</returns>
    /// <exception cref="Exception">Thrown when ffprobe cannot start or exits with an error.</exception>
    public static async Task<string> ReadMetadataAsync(string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null) throw new Exception("Failed to start ffprobe process.");

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"ffprobe failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}
