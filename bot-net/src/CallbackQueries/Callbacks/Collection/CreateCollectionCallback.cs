using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class CreateCollectionCallback : ICallbackQuery
{
    public const string Id = "create-collection";

    private CreateCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        // TODO: Implement logic
        throw new NotImplementedException();
    }

    public Task ExecuteAsync(Message? message)
    {
        // TODO: Implement logic
        throw new NotImplementedException();
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CreateCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}