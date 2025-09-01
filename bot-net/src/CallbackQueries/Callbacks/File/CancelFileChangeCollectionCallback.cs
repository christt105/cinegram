using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class CancelPendingActionCallback : ICallbackQuery
{
    public const string Id = "cancel-pending-action";
    private readonly int _fileId;
    private readonly PendingActionHandler _pendingActionHandler;
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private CancelPendingActionCallback(int fileId, PendingActionHandler pendingActionHandler, ApiClient apiClient,
        WTelegram.Bot bot)
    {
        _fileId = fileId;
        _pendingActionHandler = pendingActionHandler;
        _apiClient = apiClient;
        _bot = bot;
    }

    public async Task ExecuteAsync(Message? message)
    {
        _pendingActionHandler.Clear();
        await new ShowFileCallback(_fileId, _bot, _apiClient).ExecuteAsync(message);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CancelPendingActionCallback(int.Parse(fields[0]), dispatcher.PendingActionHandler,
            dispatcher.ApiClient, dispatcher.Bot);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}