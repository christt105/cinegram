using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;
using Cinegram.Watch;

namespace Cinegram.TelegramBot.Commands;

/// <summary>
/// Forces a sweep of the downloads folder against the backend's rows, for when the
/// FileSystemWatcher missed a file. Whatever it finds gets the usual Confirm/Correct message.
/// </summary>
public class RescanDownloadsCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly WatchedFolderService _watchedFolder;

    public RescanDownloadsCommand(WTelegram.Bot bot, WatchedFolderService watchedFolder)
    {
        _bot = bot;
        _watchedFolder = watchedFolder;
    }

    public async Task Execute(string[] args, Message msg)
    {
        await _bot.SendMessage(msg.Chat.Id, "🔍 Scanning the downloads folder…");

        try
        {
            var diff = await _watchedFolder.RescanAsync(CancellationToken.None);
            await _bot.SendMessage(msg.Chat.Id,
                $"✅ Scan done: {diff.New.Count} new file(s) found, {diff.Missing.Count} no longer on disk.");
        }
        catch (Exception ex)
        {
            Log.Error("Rescan command failed", ex);
            await _bot.SendMessage(msg.Chat.Id, "❌ Failed to scan the downloads folder.");
        }
    }

    public string Key => "/rescan";
    public string Description => "Scan the downloads folder for files the watcher missed.";
    public string Usage => "/rescan";
}
