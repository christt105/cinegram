using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

public class CreateCollectionCallback : ICallbackQuery
{
    private CreateCollectionCallback(int parse, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        throw new NotImplementedException();
    }

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public const string Id = "create-collection";

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new CreateCollectionCallback(int.Parse(fields[0]), botBot, botApiClient);
    }
}