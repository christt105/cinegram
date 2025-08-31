using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class EditCollectionCallback : ICallbackQuery
{
    public const string Id = "edit-collection";

    public EditCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        // TODO: Implement logic
        throw new NotImplementedException();
    }

    public Task ExecuteAsync(Message? message)
    {
        // TODO: Implement logic
        throw new NotImplementedException();
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new EditCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}