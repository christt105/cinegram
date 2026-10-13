using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Cinegram.Data;
using Cinegram.Library;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Collection;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class ConfirmDeleteFileCallback : ICallbackQuery
{
    public const string Id = "confirm-delete-file";
    private int _fileId;
    private WTelegram.Bot _bot;
    private Catalog _catalog;

    public ConfirmDeleteFileCallback(int fileId, WTelegram.Bot bot, Catalog catalog)
    {
        _fileId = fileId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var file = await _catalog.GetFileAsync(_fileId);
        var result = await _catalog.DeleteFileAsync(_fileId);

        var text = result
            ? $"File {_fileId} deleted successfully."
            : $"Failed to delete the file {_fileId}.";

        await _bot.EditMessageText(message.Chat.Id, message.MessageId, text, replyMarkup: file != null && true ?
        new[]{
            InlineKeyboardButton.WithCallbackData("Back to files", ShowCollectionCallback.Pack(file.CollectionId))
        } : null);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ConfirmDeleteFileCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}