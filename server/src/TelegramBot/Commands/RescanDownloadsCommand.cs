using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;

namespace Cinegram.TelegramBot.Commands;

/// <summary>
/// Forces a sweep of the downloads folder against the backend's rows, for when the
/// FileSystemWatcher missed a file. Whatever it finds gets the usual Confirm/Correct message.
/// </summary>
public class RescanDownloadsCommand : ICommand
{
    private readonly BotDispatcher _dispatcher;

    public RescanDownloadsCommand(BotDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task Execute(string[] args, Message msg)
    {
        await _dispatcher.Bot.SendMessage(msg.Chat.Id, "🔍 Scanning the downloads folder…");

        try
        {
            var diff = await _dispatcher.WatchedFolder.RescanAsync(CancellationToken.None);
            await _dispatcher.Bot.SendMessage(msg.Chat.Id,
                $"✅ Scan done: {diff.New.Count} new file(s) found, {diff.Missing.Count} no longer on disk.");
        }
        catch (Exception ex)
        {
            Log.Error("Rescan command failed", ex);
            await _dispatcher.Bot.SendMessage(msg.Chat.Id, "❌ Failed to scan the downloads folder.");
        }
    }

    public string Key => "/rescan";
    public string Description => "Scan the downloads folder for files the watcher missed.";
    public string Usage => "/rescan";
}
