using Bot.Handlers;
using Bot.Commands;
using Bot.Services;
using Bot.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks;

[Callback(Id)]
public class OrphansCallback : ICallbackQuery
{
    public const string Id = "orphans";

    private readonly BotDispatcher _dispatcher;
    private readonly string _action;
    private readonly int _page;
    private readonly string? _targetIdStr;

    private OrphansCallback(BotDispatcher dispatcher, string action, int page, string? targetIdStr)
    {
        _dispatcher = dispatcher;
        _action = action;
        _page = page;
        _targetIdStr = targetIdStr;
    }

    public async Task ExecuteAsync(Message? message)
    {
        if (message == null) return;

        var bot = _dispatcher.Bot;
        var apiClient = _dispatcher.ApiClient;

        if (_action == "page" || _action == "refresh")
        {
            await RefreshMessage(message, _page);
        }
        else if (_action == "confirm_del" && int.TryParse(_targetIdStr, out var delConfirmId))
        {
            var buttons = new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🗑️ Confirm Deletion", Pack("delete", _page, delConfirmId.ToString())),
                    InlineKeyboardButton.WithCallbackData("✕ Cancel", Pack("refresh", _page))
                }
            };
            await bot.EditMessageText(message.Chat.Id, message.MessageId, 
                $"⚠️ Are you sure you want to delete collection ID <b>{delConfirmId}</b>?\nThis action cannot be undone.",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons));
        }
        else if (_action == "delete" && int.TryParse(_targetIdStr, out var delId))
        {
            var ok = await apiClient.DeleteCollectionAsync(delId);
            if (ok)
            {
                await bot.SendMessage(message.Chat.Id, "✅ Collection deleted successfully.");
            }
            else
            {
                await bot.SendMessage(message.Chat.Id, "❌ Error deleting the collection.");
            }
            await RefreshMessage(message, _page);
        }
        else if (_action == "ident" && int.TryParse(_targetIdStr, out var identId))
        {
            await bot.EditMessageText(message.Chat.Id, message.MessageId,
                $"🔍 <b>Identifying Collection #{identId}</b>\n\nReply to this message with the series/movie name from TMDB, or enter the numeric ID directly if you already know it:",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[] {
                    InlineKeyboardButton.WithCallbackData("✕ Cancel", Pack("refresh", _page))
                }));

            await _dispatcher.PendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
                "ident-orphan",
                message.Chat.Id,
                message.MessageId,
                async text =>
                {
                    await ProcessManualInput(message, identId, text, _page);
                },
                async () =>
                {
                    await RefreshMessage(message, _page);
                }));
        }
        else if (_action == "link" && _targetIdStr != null)
        {
            var parts = _targetIdStr.Split("_");
            if (parts.Length == 2 && int.TryParse(parts[0], out var colId) && int.TryParse(parts[1], out var tmdbId))
            {
                var ok = await apiClient.IdentifyCollectionAsync(colId, tmdbId);
                if (ok)
                {
                    await bot.SendMessage(message.Chat.Id, "✅ Collection linked successfully.");
                }
                else
                {
                    await bot.SendMessage(message.Chat.Id, "❌ Error linking the collection.");
                }
                
                await RefreshMessage(message, _page);
            }
        }
    }

    private async Task ProcessManualInput(Message originalMessage, int collectionId, string text, int returnPage)
    {
        var bot = _dispatcher.Bot;
        var apiClient = _dispatcher.ApiClient;

        if (int.TryParse(text.Trim(), out var tmdbId))
        {
            var ok = await apiClient.IdentifyCollectionAsync(collectionId, tmdbId);
            if (ok)
            {
                await bot.SendMessage(originalMessage.Chat.Id, "✅ Collection linked successfully.");
            }
            else
            {
                await bot.SendMessage(originalMessage.Chat.Id, "❌ Error linking the collection.");
            }
            await RefreshMessage(originalMessage, returnPage);
            return;
        }

        await bot.SendMessage(originalMessage.Chat.Id, $"🔍 Searching for \"{text}\" on TMDB...");
        try
        {
            var results = await apiClient.SearchTmdbAsync(text);
            if (results == null || results.Count == 0)
            {
                await bot.SendMessage(originalMessage.Chat.Id, "❌ No results found on TMDB.");
                await RefreshMessage(originalMessage, returnPage);
                return;
            }

            var buttons = new List<InlineKeyboardButton[]>();
            foreach (var r in results.Take(5))
            {
                var typeLabel = r.MediaType == "movie" ? "🎬" : "📺";
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{typeLabel} {r.Title} ({r.Year})", 
                        Pack("link", returnPage, $"{collectionId}_{r.Id}")
                    )
                });
            }
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("✕ Cancel", Pack("refresh", returnPage)) });

            await bot.SendMessage(originalMessage.Chat.Id, 
                $"Choose the correct result to link:", 
                replyMarkup: new InlineKeyboardMarkup(buttons));
        }
        catch (Exception ex)
        {
            Log.Error("Failed to search TMDB or link collection", ex);
            await bot.SendMessage(originalMessage.Chat.Id, "❌ Connection error while searching TMDB.");
            await RefreshMessage(originalMessage, returnPage);
        }
    }

    private async Task RefreshMessage(Message message, int page)
    {
        try
        {
            var (text, markup) = await OrphansMessageBuilder.BuildOrphansMessage(_dispatcher.ApiClient, page);
            await _dispatcher.Bot.EditMessageText(message.Chat.Id, message.MessageId, text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: markup);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to refresh orphans list", ex);
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var action = fields.Length > 0 ? fields[0] : "refresh";
        var page = fields.Length > 1 && int.TryParse(fields[1], out var p) ? p : 0;
        var targetIdStr = fields.Length > 2 ? fields[2] : null;
        return new OrphansCallback(dispatcher, action, page, targetIdStr);
    }

    public static string Pack(string action, int page, string? targetIdStr = null)
    {
        var fields = new List<string> { action, page.ToString() };
        if (targetIdStr != null) fields.Add(targetIdStr);
        return CallbackDataPacker.Pack(Id, fields.ToArray());
    }
}
