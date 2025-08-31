using Bot.CallbackQueries;
using Bot.CallbackQueries.Callbacks;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

[Callback(Id)]
public class ConfirmDeleteFileCallback : ICallbackQuery
{
    public const string Id = "confirm-delete-file";
    private int _fileId;
    private WTelegram.Bot _bot;
    private ApiClient _apiClient;

    public ConfirmDeleteFileCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        var file = await _apiClient.GetFileAsync(_fileId);
        var result = await _apiClient.DeleteFileAsync(_fileId);

        var text = result
            ? $"File {_fileId} deleted successfully."
            : $"Failed to delete the file {_fileId}.";

        await _bot.EditMessageText(message.Chat.Id, message.MessageId, text, replyMarkup: file != null && file.CollectionId.HasValue ?
        new[]{
            InlineKeyboardButton.WithCallbackData("Back to files", SeeCollectionFilesCallback.Pack(file.CollectionId.Value))
        } : null);
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot bot, ApiClient apiClient)
    {
        return new ConfirmDeleteFileCallback(int.Parse(fields[0]), bot, apiClient);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}