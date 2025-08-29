using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

public class DeleteCollectionCallback : ICallbackQuery
{
    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack("delete-collection", [collectionId.ToString()]);
    }
}