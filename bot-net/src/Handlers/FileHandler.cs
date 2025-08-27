using Telegram.Bot.Types.Enums;
using WTelegram.Types;

namespace TelegramBot.Handlers;

public class FileHandler
{
    private readonly WTelegram.Bot _bot;

    public FileHandler(WTelegram.Bot bot)
    {
        _bot = bot;
    }

    public async Task Handle(Message msg, UpdateType type)
    {

    }
}