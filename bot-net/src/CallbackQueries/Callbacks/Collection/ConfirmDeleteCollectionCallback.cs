using Bot.CallbackQueries.Callbacks.Movie;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class ConfirmDeleteCollectionCallback : ICallbackQuery
{
    public const string Id = "confirmDeleteCollection";

    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _collectionId;

    public ConfirmDeleteCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _collectionId = collectionId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _apiClient.GetCollectionAsync(_collectionId);
        var result = await _apiClient.DeleteCollectionAsync(_collectionId);

        var text = result
            ? $"Collection {_collectionId} deleted successfully."
            : $"Failed to delete the collection {_collectionId}.";

        await _bot.EditMessageText(message.Chat.Id, message.MessageId, text,
            replyMarkup: collection != null && collection.MovieId.HasValue
                ? new[]
                {
                    InlineKeyboardButton.WithCallbackData("Back to collections",
                        SeeMovieCollectionsCallback.Pack(collection.MovieId.Value))
                }
                : null);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ConfirmDeleteCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }
}