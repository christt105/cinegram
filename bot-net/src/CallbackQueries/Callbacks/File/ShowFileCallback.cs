using Bot.CallbackQueries.Callbacks.Collection;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class ShowFileCallback : ICallbackQuery
{
    public const string Id = "show-file";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;

    public ShowFileCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var file = await _apiClient.GetFileAsync(_fileId);

        if (file is null)
        {
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, $"File {_fileId} not found");
            return;
        }

        var collection = await _apiClient.GetCollectionAsync(file.CollectionId.Value);

        var text = $"""
                    File

                    Collection: {collection?.Name ?? "Not found"}
                    Id: {_fileId}
                    Filename: {file.FileName}
                    Size: {Beautify.FormatSize(file.FileSize)}
                    Created At: {file.CreatedAt}
                    """;

        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Delete File", DeleteFileCallback.Parse(_fileId))
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Change Collection", FileChangeCollectionCallback.Parse(_fileId))
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\u2b05\ufe0f Back to collection",
                    SeeCollectionFilesCallback.Pack(file.CollectionId.Value))
            }
        };

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, replyMarkup: buttons);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ShowFileCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}