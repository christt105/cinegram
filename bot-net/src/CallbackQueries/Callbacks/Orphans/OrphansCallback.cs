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
                    InlineKeyboardButton.WithCallbackData("🗑️ Confirmar Borrado", Pack("delete", _page, delConfirmId.ToString())),
                    InlineKeyboardButton.WithCallbackData("✕ Cancelar", Pack("refresh", _page))
                }
            };
            await bot.EditMessageText(message.Chat.Id, message.MessageId, 
                $"⚠️ ¿Estás seguro de que quieres borrar la colección ID <b>{delConfirmId}</b>?\nEsta acción no se puede deshacer.",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons));
        }
        else if (_action == "delete" && int.TryParse(_targetIdStr, out var delId))
        {
            var ok = await apiClient.DeleteCollectionAsync(delId);
            if (ok)
            {
                await bot.SendMessage(message.Chat.Id, "✅ Colección eliminada correctamente.");
            }
            else
            {
                await bot.SendMessage(message.Chat.Id, "❌ Error al eliminar la colección.");
            }
            await RefreshMessage(message, _page);
        }
        else if (_action == "ident" && int.TryParse(_targetIdStr, out var identId))
        {
            await bot.EditMessageText(message.Chat.Id, message.MessageId,
                $"🔍 <b>Identificando Colección #{identId}</b>\n\nResponde a este mensaje escribiendo el nombre de la serie/película en TMDB, o introduce directamente el ID numérico si ya lo conoces:",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[] {
                    InlineKeyboardButton.WithCallbackData("✕ Cancelar", Pack("refresh", _page))
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
                    await bot.SendMessage(message.Chat.Id, "✅ Colección vinculada correctamente.");
                }
                else
                {
                    await bot.SendMessage(message.Chat.Id, "❌ Error al vincular la colección.");
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
                await bot.SendMessage(originalMessage.Chat.Id, "✅ Colección vinculada correctamente.");
            }
            else
            {
                await bot.SendMessage(originalMessage.Chat.Id, "❌ Error al vincular la colección.");
            }
            await RefreshMessage(originalMessage, returnPage);
            return;
        }

        await bot.SendMessage(originalMessage.Chat.Id, $"🔍 Buscando \"{text}\" en TMDB...");
        try
        {
            var results = await apiClient.SearchTmdbAsync(text);
            if (results == null || results.Count == 0)
            {
                await bot.SendMessage(originalMessage.Chat.Id, "❌ No se han encontrado resultados en TMDB.");
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
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("✕ Cancelar", Pack("refresh", returnPage)) });

            await bot.SendMessage(originalMessage.Chat.Id, 
                $"Elige el resultado correcto para vincular:", 
                replyMarkup: new InlineKeyboardMarkup(buttons));
        }
        catch (Exception ex)
        {
            Log.Error("Failed to search TMDB or link collection", ex);
            await bot.SendMessage(originalMessage.Chat.Id, "❌ Error de conexión al buscar en TMDB.");
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
