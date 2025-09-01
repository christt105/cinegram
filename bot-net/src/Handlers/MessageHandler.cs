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

    public Task Handle(Message msg, UpdateType type)
    {
        return Task.CompletedTask;
    }
}