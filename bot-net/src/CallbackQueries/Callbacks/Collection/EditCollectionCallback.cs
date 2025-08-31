using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class EditCollectionCallback : ICallbackQuery
{
    public const string Id = "edit-collection";

    public EditCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        throw new NotImplementedException();
    }

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new EditCollectionCallback(int.Parse(fields[0]), botBot, botApiClient);
    }
}