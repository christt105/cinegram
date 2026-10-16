using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Cinegram.Data;
using Cinegram.Library;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Movie;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class ConfirmDeleteCollectionCallback : ICallbackQuery
{
    public const string Id = "confirmDeleteCollection";

    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;

    private readonly int _collectionId;

    public ConfirmDeleteCollectionCallback(int collectionId, WTelegram.Bot bot, Catalog catalog)
    {
        _collectionId = collectionId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _catalog.GetCollectionAsync(_collectionId);
        var result = await _catalog.DeleteCollectionAsync(_collectionId);

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

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot bot, Catalog catalog)
    {
        return new ConfirmDeleteCollectionCallback(int.Parse(fields[0]), bot, catalog);
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }
}