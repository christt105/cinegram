using Telegram.Bot.Types;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot.Commands;
using Cinegram.Transfers;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Queue;

[Callback(Id)]
public class QueueCallback : ICallbackQuery
{
    public const string Id = "queue";
    
    private readonly TransferTasks _tasks;
    private readonly WTelegram.Bot _bot;
    private readonly string _action;
    private readonly string? _taskIdStr;

    private QueueCallback(WTelegram.Bot bot, TransferTasks tasks, string action, string? taskIdStr)
    {
        _bot = bot;
        _tasks = tasks;
        _action = action;
        _taskIdStr = taskIdStr;
    }

    public async Task ExecuteAsync(Message? message)
    {
        if (message == null) return;

        // Perform the requested queue action
        if (_action == "cancel_up" && int.TryParse(_taskIdStr, out var upId))
        {
            await _tasks.DeleteUploadAsync(upId);
        }
        else if (_action == "cancel_down" && int.TryParse(_taskIdStr, out var downId))
        {
            await _tasks.DeleteDownloadAsync(downId);
        }
        else if (_action == "retry_up" && int.TryParse(_taskIdStr, out var retryUpId))
        {
            await _tasks.RetryUploadAsync(retryUpId);
        }
        else if (_action == "retry_down" && int.TryParse(_taskIdStr, out var retryDownId))
        {
            await _tasks.RetryDownloadAsync(retryDownId);
        }
        
        // Regenerate and edit the message
        try
        {
            var (text, markup) = await QueueMessageBuilder.BuildQueueMessage(_tasks);
            await _bot.EditMessageText(message.Chat.Id, message.MessageId, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: markup);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to refresh queue message", ex);
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var action = fields.Length > 0 ? fields[0] : "refresh";
        var taskIdStr = fields.Length > 1 ? fields[1] : null;
        return new QueueCallback(dispatcher.Bot, dispatcher.Transfers, action, taskIdStr);
    }

    public static string Pack(string action, string? taskIdStr = null)
    {
        var fields = new List<string> { action };
        if (taskIdStr != null) fields.Add(taskIdStr);
        return CallbackDataPacker.Pack(Id, fields.ToArray());
    }
}
