using System.Text.RegularExpressions;
using Bot.Models;
using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public partial class ImportHandler
{
    private static readonly string[] VideoExtensions =
        [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm"];

    [GeneratedRegex(@"\[tmdbid-(\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex TmdbIdPattern();

    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;

    private static volatile bool _running;

    public ImportHandler(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Run(long chatId, string moviesDir, string showsDir)
    {
        if (_running)
        {
            await _bot.SendMessage(chatId, "⚠️ An import is already in progress.");
            return;
        }

        _running = true;
        try
        {
            await RunInternal(chatId, moviesDir, showsDir);
        }
        finally
        {
            _running = false;
        }
    }

    private async Task RunInternal(long chatId, string moviesDir, string showsDir)
    {
        var files = Discover(moviesDir, showsDir);

        if (files.Count == 0)
        {
            await _bot.SendMessage(chatId, "No video files found in the import directories.");
            return;
        }

        var progress = await _bot.SendMessage(chatId,
            $"📂 Found {files.Count} file(s). Starting import...");

        var success = 0;
        var failed = new List<string>();

        for (var i = 0; i < files.Count; i++)
        {
            var (path, tmdbId) = files[i];
            var name = Path.GetFileName(path);

            if (i % 3 == 0 || i == files.Count - 1)
                await SafeEdit(chatId, progress.MessageId,
                    $"📤 Importing {i + 1}/{files.Count}…\n{name}");

            try
            {
                await UploadOne(chatId, path, tmdbId);
                success++;
            }
            catch (Exception ex)
            {
                Log.Error($"Import failed for {path}", ex);
                failed.Add(name);
            }
        }

        var summary = $"✅ Import complete: {success}/{files.Count} files uploaded.";
        if (failed.Count > 0)
            summary += "\n\n❌ Failed:\n" + string.Join("\n", failed.Select(f => $"• {f}"));

        await SafeEdit(chatId, progress.MessageId, summary);
    }

    private async Task UploadOne(long chatId, string filePath, int? tmdbId)
    {
        var info = new FileInfo(filePath);

        await using var stream = info.OpenRead();
        var sent = await _bot.SendDocument(
            chatId,
            new InputFileStream(stream, info.Name),
            caption: info.Name);

        var uploadFile = new UploadFile
        {
            MessageId = sent.MessageId,
            FileName = info.Name,
            FileSize = info.Length,
            MimeType = GuessMime(filePath),
            UploadDate = DateTime.UtcNow.ToString("O"),
            TmdbId = tmdbId
        };

        await _apiClient.UploadAsync(uploadFile);
    }

    private static List<(string Path, int? TmdbId)> Discover(string moviesDir, string showsDir)
    {
        var result = new List<(string, int?)>();
        ScanDir(moviesDir, extractTmdb: true, result);
        ScanDir(showsDir, extractTmdb: false, result);
        return result;
    }

    private static void ScanDir(string dir, bool extractTmdb, List<(string, int?)> result)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                     .Where(f => VideoExtensions.Contains(
                         Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            result.Add((file, extractTmdb ? ExtractTmdbId(file) : null));
        }
    }

    // Walks up the directory tree from the file looking for [tmdbid-X] in any folder name.
    private static int? ExtractTmdbId(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            var match = TmdbIdPattern().Match(Path.GetFileName(dir) ?? "");
            if (match.Success) return int.Parse(match.Groups[1].Value);
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string GuessMime(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mkv" => "video/x-matroska",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            _ => "video/octet-stream"
        };

    private async Task SafeEdit(long chatId, int msgId, string text)
    {
        try { await _bot.EditMessageText(chatId, msgId, text); }
        catch (Exception ex) { Log.Error($"Failed to edit progress message: {ex.Message}"); }
    }
}
