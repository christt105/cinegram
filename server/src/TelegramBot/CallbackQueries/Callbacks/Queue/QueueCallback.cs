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

        Notice = await PerformAsync();

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

    public string? Notice { get; private set; }

    /// <summary>Runs the button's action and returns what to tell the user, or null for a refresh.</summary>
    private async Task<string?> PerformAsync()
    {
        if (!int.TryParse(_taskIdStr, out var taskId)) return null;

        var (done, notice) = _action switch
        {
            "cancel_up" => (await _tasks.DeleteUploadAsync(taskId), "Upload cancelled"),
            "cancel_down" => (await _tasks.DeleteDownloadAsync(taskId), "Download cancelled"),
            "retry_up" => (await _tasks.RetryUploadAsync(taskId), "Retrying upload"),
            "retry_down" => (await _tasks.RetryDownloadAsync(taskId), "Retrying download"),
            _ => (true, (string?)null),
        };
        return done ? notice : "That task is no longer in the queue.";
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot bot, TransferTasks transfers)
    {
        var action = fields.Length > 0 ? fields[0] : "refresh";
        var taskIdStr = fields.Length > 1 ? fields[1] : null;
        return new QueueCallback(bot, transfers, action, taskIdStr);
    }

    public static string Pack(string action, string? taskIdStr = null)
    {
        var fields = new List<string> { action };
        if (taskIdStr != null) fields.Add(taskIdStr);
        return CallbackDataPacker.Pack(Id, fields.ToArray());
    }
}
