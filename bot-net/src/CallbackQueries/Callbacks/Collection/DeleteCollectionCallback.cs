using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class DeleteCollectionCallback : ICallbackQuery
{
    public const string Id = "delete-collection";
    public Task ExecuteAsync(Message? message)
    {
        // TODO: Implement logic
        throw new NotImplementedException();
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack("delete-collection", [collectionId.ToString()]);
    }
}