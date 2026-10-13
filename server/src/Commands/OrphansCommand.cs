using System.Text;
using Cinegram.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;

namespace Cinegram.Commands;

public static class OrphansMessageBuilder
{
    public static async Task<(string Text, InlineKeyboardMarkup Markup)> BuildOrphansMessage(Catalog catalog, int page)
    {
        var orphans = await catalog.ListOrphansAsync();

        if (orphans.Count == 0)
        {
            return ("🎉 <b>No anomalies or orphaned collections found in the database!</b>", 
                    new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("🔄 Refresh", CallbackQueries.Callbacks.OrphansCallback.Pack("refresh", 0)) }));
        }

        int pageSize = 5;
        int totalPages = (orphans.Count + pageSize - 1) / pageSize;
        if (page < 0) page = 0;
        if (page >= totalPages) page = totalPages - 1;

        int startIndex = page * pageSize;
        var currentOrphans = orphans.Skip(startIndex).Take(pageSize).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 <b>Orphaned Collections (Page {page + 1}/{totalPages})</b>");
        sb.AppendLine("These collections are not associated with any movie or series:\n");

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var col in currentOrphans)
        {
            sb.AppendLine($"📦 <b>{col.Name}</b>");
            sb.AppendLine($"   ID: {col.Id} | Quality: {col.Quality ?? "Auto"}");
            sb.AppendLine();

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"🔍 Identify #{col.Id}", CallbackQueries.Callbacks.OrphansCallback.Pack("ident", page, col.Id.ToString())),
                InlineKeyboardButton.WithCallbackData($"🗑️ Delete", CallbackQueries.Callbacks.OrphansCallback.Pack("confirm_del", page, col.Id.ToString()))
            });
        }

        // Navigation Row
        var navRow = new List<InlineKeyboardButton>();
        if (page > 0)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", CallbackQueries.Callbacks.OrphansCallback.Pack("page", page - 1)));
        }
        
        navRow.Add(InlineKeyboardButton.WithCallbackData("🔄 Refresh", CallbackQueries.Callbacks.OrphansCallback.Pack("refresh", page)));

        if (page < totalPages - 1)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", CallbackQueries.Callbacks.OrphansCallback.Pack("page", page + 1)));
        }

        buttons.Add(navRow.ToArray());

        return (sb.ToString(), new InlineKeyboardMarkup(buttons));
    }
}

public class OrphansCommand : ICommand
{
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;

    public OrphansCommand(WTelegram.Bot bot, Catalog catalog)
    {
        _bot = bot;
        _catalog = catalog;
    }

    public async Task Execute(string[] args, Message msg)
    {
        try
        {
            var (text, markup) = await OrphansMessageBuilder.BuildOrphansMessage(_catalog, 0);
            await _bot.SendMessage(msg.Chat.Id, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: markup,
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to execute /orphans command", ex);
            await _bot.SendMessage(msg.Chat.Id, "❌ Failed to fetch orphans.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }

    public string Key => "/orphans";
    public string Description => "Lists orphaned collections that need TMDB identification";
    public string Usage => "/orphans";
}
