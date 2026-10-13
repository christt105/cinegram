using Cinegram.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Cinegram.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class DeleteFileCallback : ICallbackQuery
{
    public const string Id = "delete-file";
    private int _fileId;
    private WTelegram.Bot _bot;
    private Catalog _catalog;

    public DeleteFileCallback(int fileId, WTelegram.Bot bot, Catalog catalog)
    {
        _fileId = fileId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.EditMessageText(
            message.Chat.Id,
            message.MessageId,
            "Are you sure you want to delete this file?",
            replyMarkup: new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", ShowFileCallback.Pack(_fileId)),
                    InlineKeyboardButton.WithCallbackData("Delete", ConfirmDeleteFileCallback.Pack(_fileId)),
                }
            });
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new DeleteFileCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog);
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}