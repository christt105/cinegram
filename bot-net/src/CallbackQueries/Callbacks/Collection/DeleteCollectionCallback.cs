using Bot.CallbackQueries.Callbacks.File;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class DeleteCollectionCallback : ICallbackQuery
{
    public const string Id = "delete-collection";
    private readonly WTelegram.Bot _bot;
    private readonly int _collectionId;

    public DeleteCollectionCallback(int collectionId, WTelegram.Bot bot)
    {
        _collectionId = collectionId;
        _bot = bot;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            "Are you sure you want to delete this collection?",
            replyMarkup: new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", SeeCollectionFilesCallback.Pack(_collectionId)),
                    InlineKeyboardButton.WithCallbackData("Delete", ConfirmDeleteCollectionCallback.Pack(_collectionId))
                }
            });
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack("delete-collection", [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new DeleteCollectionCallback(int.Parse(fields[0]), dispatcher.Bot);
    }
}