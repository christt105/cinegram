using Bot.Handlers;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

[Callback(Id)]
public class CancelPendingActionCallback : ICallbackQuery
{
    public const string Id = "cancel-pending-action";
    private readonly PendingActionHandler _pendingActionHandler;

    private CancelPendingActionCallback(PendingActionHandler pendingActionHandler)
    {
        _pendingActionHandler = pendingActionHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _pendingActionHandler.CancelPendingAction();
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CancelPendingActionCallback(dispatcher.PendingActionHandler);
    }

    public static string Pack()
    {
        return CallbackDataPacker.Pack(Id, []);
    }
}