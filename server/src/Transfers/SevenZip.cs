using System.Diagnostics;

namespace Cinegram.Transfers;

/// <summary>
/// Runs 7z for the transfers: extracting a downloaded archive and cutting a large upload into
/// store-only volumes. Cancelling kills the process, so a cancelled transfer does not keep
/// extracting or packing gigabytes in the background.
/// </summary>
public static class SevenZip
{
    public static Task ExtractAsync(string archivePath, string outputDir, CancellationToken ct) =>
        RunAsync("7z", $"x \"{archivePath}\" -o\"{outputDir}\" -y", "extraction", ct);

    public static Task SplitAsync(string filePath, string archivePath, long splitLimitBytes, CancellationToken ct) =>
        RunAsync("7z", $"a -mx0 -v{splitLimitBytes / 1_000_000}m \"{archivePath}\" \"{filePath}\"", "splitting", ct);

    /// <summary>Throws with the tool's own error output when it exits with a non-zero code.</summary>
    public static async Task RunAsync(string fileName, string arguments, string what, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new Exception($"Failed to start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        await output;
        if (process.ExitCode != 0)
            throw new Exception($"{fileName} {what} failed with exit code {process.ExitCode}: {await error}");
    }
}
