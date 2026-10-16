using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;

namespace Cinegram.TelegramBot.Commands;

public class StartCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly AppVersion _version;

    public StartCommand(WTelegram.Bot bot, AppVersion version)
    {
        _bot = bot;
        _version = version;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var message = $"👋 Hello! I'm your Media Library bot (v{_version.Current}). Send me a file or use /help for commands.";
        await _bot.SendMessage(msg.Chat.Id, message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/start";
    public string Description => "Welcome message";
    public string Usage => "/start";
}