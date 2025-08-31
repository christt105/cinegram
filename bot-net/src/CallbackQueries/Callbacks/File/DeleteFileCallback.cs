using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class DeleteFileCallback : ICallbackQuery
{
    public const string Id = "delete-file";
    private int _fileId;
    private WTelegram.Bot _bot;
    private ApiClient _apiClient;

    public DeleteFileCallback(int v, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        _fileId = v;
        _bot = botBot;
        _apiClient = botApiClient;
    }

    public async Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
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

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new DeleteFileCallback(int.Parse(fields[0]), botBot, botApiClient);
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}