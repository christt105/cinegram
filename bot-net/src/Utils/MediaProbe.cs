using System.Diagnostics;

namespace Bot.Utils;

/// <summary>
/// Reads technical metadata from media files with ffprobe.
/// </summary>
public static class MediaProbe
{
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
