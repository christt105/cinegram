using Bot.Services;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class BackupCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;

    public BackupCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        await _bot.SendMessage(msg.Chat.Id, "📦 Creating database backup...");

        var data = await _apiClient.DownloadBackupAsync();
        if (data == null)
        {
            await _bot.SendMessage(msg.Chat.Id, "❌ Failed to create the backup.");
            return;
        }

        var filename = $"cinegram-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
        using var stream = new MemoryStream(data);
        await _bot.SendDocument(msg.Chat.Id,
            new InputFileStream(stream, filename),
            caption: "🗄️ Cinegram database backup",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/backup";
    public string Description => "Send a backup of the database.";
    public string Usage => "/backup";
}
