using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Queue;
using Cinegram.Transfers;

namespace Cinegram.TelegramBot.Commands;

public static class QueueMessageBuilder
{
    public static async Task<(string Text, InlineKeyboardMarkup Markup)> BuildQueueMessage(TransferTasks tasks)
    {
        var uploads = await tasks.UploadQueueAsync();
        var downloads = await tasks.DownloadQueueAsync();

        var sb = new StringBuilder();
        sb.AppendLine("⏳ <b>Active Transfers</b>\n");

        var buttons = new List<InlineKeyboardButton[]>();

        // 1. Uploads Section
        sb.AppendLine("<b>Uploads (To Telegram):</b>");
        if (uploads.Count == 0)
        {
            sb.AppendLine("<i>No active uploads.</i>");
        }
        else
        {
            foreach (var task in uploads)
            {
                var title = $"{task.Title}{(task.Year.HasValue ? $" ({task.Year})" : "")}";
                sb.AppendLine($"• {title} - {task.Progress}% [<b>{task.Status.ToUpper()}</b>]");
                if (!string.IsNullOrEmpty(task.ErrorMessage))
                {
                    sb.AppendLine($"  ❌ <i>Error: {task.ErrorMessage}</i>");
                }

                // Add buttons for this task
                var cancelBtn = InlineKeyboardButton.WithCallbackData($"❌ Cancel {task.Title}", CallbackQueries.Callbacks.Queue.QueueCallback.Pack("cancel_up", task.Id.ToString()));
                if (task.Status == "failed")
                {
                    buttons.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData($"🔄 Retry {task.Title}", CallbackQueries.Callbacks.Queue.QueueCallback.Pack("retry_up", task.Id.ToString())),
                        cancelBtn
                    });
                }
                else
                {
                    buttons.Add(new[] { cancelBtn });
                }
            }
        }

        sb.AppendLine();

        // 2. Downloads Section
        sb.AppendLine("<b>Downloads (To Local Storage):</b>");
        if (downloads.Count == 0)
        {
            sb.AppendLine("<i>No active downloads.</i>");
        }
        else
        {
            foreach (var task in downloads)
            {
                sb.AppendLine($"• {task.Title} - {task.Progress}% [<b>{task.Status.ToUpper()}</b>]");
                if (!string.IsNullOrEmpty(task.ErrorMessage))
                {
                    sb.AppendLine($"  ❌ <i>Error: {task.ErrorMessage}</i>");
                }

                // Add buttons for this task
                var cancelBtn = InlineKeyboardButton.WithCallbackData($"❌ Cancel {task.Title}", CallbackQueries.Callbacks.Queue.QueueCallback.Pack("cancel_down", task.Id.ToString()));
                if (task.Status == "failed")
                {
                    buttons.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData($"🔄 Retry {task.Title}", CallbackQueries.Callbacks.Queue.QueueCallback.Pack("retry_down", task.Id.ToString())),
                        cancelBtn
                    });
                }
                else
                {
                    buttons.Add(new[] { cancelBtn });
                }
            }
        }

        // Add refresh button at the bottom
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 Refresh Queue", CallbackQueries.Callbacks.Queue.QueueCallback.Pack("refresh"))
        });

        return (sb.ToString(), new InlineKeyboardMarkup(buttons));
    }
}

public class QueueCommand : ICommand
{
    private readonly TransferTasks _tasks;
    private readonly WTelegram.Bot _bot;

    public QueueCommand(WTelegram.Bot bot, TransferTasks tasks)
    {
        _bot = bot;
        _tasks = tasks;
    }

    public async Task Execute(string[] args, Message msg)
    {
        try
        {
            var (text, markup) = await QueueMessageBuilder.BuildQueueMessage(_tasks);
            await _bot.SendMessage(msg.Chat.Id, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: markup,
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to execute /queue command", ex);
            await _bot.SendMessage(msg.Chat.Id, "❌ Failed to fetch queue.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }

    public string Key => "/queue";
    public string Description => "Shows active upload and download transfers";
    public string Usage => "/queue";
}
