using Bot.Services;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class BackupCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly DatabaseBackup _backup;

    public BackupCommand(WTelegram.Bot bot, DatabaseBackup backup)
    {
        _bot = bot;
        _backup = backup;
    }

    public async Task Execute(string[] args, Message msg)
    {
        await _bot.SendMessage(msg.Chat.Id, "📦 Creating database backup...");

        Stream snapshot;
        try
        {
            snapshot = await _backup.OpenSnapshotAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Database backup failed", ex);
            await _bot.SendMessage(msg.Chat.Id, "❌ Failed to create the backup.");
            return;
        }

        await using (snapshot)
        {
            await _bot.SendDocument(msg.Chat.Id,
                new InputFileStream(snapshot, DatabaseBackup.FileName(DateTime.UtcNow)),
                caption: "🗄️ Cinegram database backup",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }

    public string Key => "/backup";
    public string Description => "Send a backup of the database.";
    public string Usage => "/backup";
}
