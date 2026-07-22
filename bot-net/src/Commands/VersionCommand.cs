using Bot.Utils;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class VersionCommand : ICommand
{
    private readonly WTelegram.Bot _bot;

    public VersionCommand(WTelegram.Bot bot)
    {
        _bot = bot;
    }

    public async Task Execute(string[] args, Message msg)
    {
        await _bot.SendMessage(msg.Chat.Id, $"🎬 Cinegram bot v{AppVersion.Current}",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/version";
    public string Description => "Show the bot version.";
    public string Usage => "/version";
}
