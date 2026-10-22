using Telegram.Bot.Types;

namespace Cinegram.TelegramBot.CallbackQueries;

public interface ICallbackQuery
{
    Task ExecuteAsync(Message? message);

    /// <summary>A short text Telegram shows over the chat once the button is answered, or null for none.</summary>
    string? Notice => null;
}
