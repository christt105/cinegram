using Bot.Utils;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class StartCommand : ICommand
{
    private readonly WTelegram.Bot _bot;

    public StartCommand(WTelegram.Bot bot)
    {
        _bot = bot;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var message = $"👋 Hello! I'm your Media Library bot (v{AppVersion.Current}). Send me a file or use /help for commands.";
        await _bot.SendMessage(msg.Chat.Id, message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/start";
    public string Description => "Welcome message";
    public string Usage => "/start";
}