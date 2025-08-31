using Telegram.Bot.Types;

namespace Bot.CallbackQueries;

public interface ICallbackQuery
{
    Task ExecuteAsync(Message? message);
}