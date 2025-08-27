using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace Bot.Handlers;

public class MessageHandler
{
    private readonly WTelegram.Bot _bot;

    public MessageHandler(WTelegram.Bot bot)
    {
        _bot = bot;
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        await _bot.SendMessage(msg.Chat.Id, "Hello",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }
}