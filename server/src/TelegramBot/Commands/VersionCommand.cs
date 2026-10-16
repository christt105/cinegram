using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;

namespace Cinegram.TelegramBot.Commands;

public class VersionCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly AppVersion _version;

    public VersionCommand(WTelegram.Bot bot, AppVersion version)
    {
        _bot = bot;
        _version = version;
    }

    public async Task Execute(string[] args, Message msg)
    {
        await _bot.SendMessage(msg.Chat.Id, $"🎬 Cinegram bot v{_version.Current}",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/version";
    public string Description => "Show the bot version.";
    public string Usage => "/version";
}
