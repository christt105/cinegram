using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class CreateCollectionCallback : ICallbackQuery
{
    public const string Id = "create-collection";

    private CreateCollectionCallback(int parse, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        throw new NotImplementedException();
    }

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new CreateCollectionCallback(int.Parse(fields[0]), botBot, botApiClient);
    }
}