using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class AddCommand : ICommand
{
    private readonly BotDispatcher _dispatcher;

    public AddCommand(BotDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var bot = _dispatcher.Bot;
        var apiClient = _dispatcher.ApiClient;

        if (args.Length > 0)
        {
            var query = string.Join(" ", args);
            await PerformSearchAndShowResults(msg, query);
            return;
        }

        await bot.SendMessage(msg.Chat.Id, 
            "➕ <b>Add Movie or Series Manually</b>\n\nType the name of what you want to search on TMDB (or type /cancel to abort):",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);

        await _dispatcher.PendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            "add-media-search",
            msg.Chat.Id,
            msg.MessageId,
            async text =>
            {
                await PerformSearchAndShowResults(msg, text);
            }));
    }

    private async Task PerformSearchAndShowResults(Message originalMessage, string query)
    {
        var bot = _dispatcher.Bot;
        var apiClient = _dispatcher.ApiClient;

        await bot.SendMessage(originalMessage.Chat.Id, $"🔍 Searching for \"{query}\" on TMDB...");

        try
        {
            var results = await apiClient.SearchTmdbAsync(query);
            if (results == null || results.Count == 0)
            {
                await bot.SendMessage(originalMessage.Chat.Id, "❌ No results found on TMDB.");
                return;
            }

            var buttons = new List<InlineKeyboardButton[]>();
            foreach (var r in results.Take(6))
            {
                var typeLabel = r.MediaType == "movie" ? "🎬" : "📺";
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{typeLabel} {r.Title} ({r.Year})", 
                        CallbackQueries.Callbacks.AddCallback.Pack("create", r.Id.ToString(), r.MediaType)
                    )
                });
            }

            await bot.SendMessage(originalMessage.Chat.Id, 
                $"Select the item you want to add to the local library:", 
                replyMarkup: new InlineKeyboardMarkup(buttons));
        }
        catch (Exception ex)
        {
            Log.Error("Failed to search TMDB for add command", ex);
            await bot.SendMessage(originalMessage.Chat.Id, "❌ Error searching TMDB.");
        }
    }

    public string Key => "/add";
    public string Description => "Search TMDB and manually add a movie or series to local database";
    public string Usage => "/add [search query]";
}
