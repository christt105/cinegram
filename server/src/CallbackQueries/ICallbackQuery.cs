using Telegram.Bot.Types;

namespace Cinegram.CallbackQueries;

public interface ICallbackQuery
{
    Task ExecuteAsync(Message? message);
}