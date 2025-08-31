using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class FileChangeCollectionCallback : ICallbackQuery
{
    public const string Id = "file-change-collection";
    private int _fileId;
    private WTelegram.Bot _bot;
    private ApiClient _apiClient;

    public FileChangeCollectionCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "Type a new collection ID for the file:");
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new FileChangeCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}