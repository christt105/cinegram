using Telegram.Bot.Types;

namespace Cinegram.TelegramBot.CallbackQueries;

public interface ICallbackQuery
{
    Task ExecuteAsync(Message? message);
}