using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Library;
using Cinegram.TelegramBot.Handlers;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class ReidentifyCollectionCallback : ICallbackQuery
{
    public const string Id = "reidentify-collection";

    private readonly int _collectionId;
    private readonly WTelegram.Bot _bot;
    private readonly Catalog _catalog;
    private readonly CatalogIdentifier _identifier;
    private readonly PendingActionHandler _pendingActionHandler;
    private readonly string? _subAction;

    public ReidentifyCollectionCallback(
        int collectionId,
        WTelegram.Bot bot,
        Catalog catalog,
        CatalogIdentifier identifier,
        PendingActionHandler pendingActionHandler,
        string? subAction = null)
    {
        _identifier = identifier;
        _collectionId = collectionId;
        _bot = bot;
        _catalog = catalog;
        _pendingActionHandler = pendingActionHandler;
        _subAction = subAction;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _catalog.GetCollectionAsync(_collectionId);
        if (collection is null)
        {
            await _bot.SendMessage(message!.Chat.Id, $"Collection {_collectionId} not found.");
            return;
        }

        if (_subAction == "auto")
        {
            await RunAuto(message!, collection.Name ?? $"Collection {_collectionId}");
            return;
        }

        if (_subAction == "manual")
        {
            await RunManual(message!, collection.Name ?? $"Collection {_collectionId}");
            return;
        }

        // Main re-identify menu
        var text = $"🔍 <b>Re-identify Collection:</b> {collection.Name}\n\nChoose how to identify this collection:";

        var buttons = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🤖 Auto-detect (from name)", Pack(_collectionId, "auto")) },
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Manual (enter TMDB ID)", Pack(_collectionId, "manual")) },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Back", EditCollectionCallback.Pack(_collectionId)) }
        };

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text,
            Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    private async Task RunAuto(Message message, string collectionName)
    {
        var progress = await _bot.SendMessage(message.Chat.Id,
            $"⏳ Auto-detecting <b>{collectionName}</b>…",
            Telegram.Bot.Types.Enums.ParseMode.Html);

        try
        {
            var success = await _identifier.ReidentifyAsync(_collectionId, new CatalogIdentifier.Request());
            var result = success
                ? "✅ Collection successfully re-identified!"
                : "❌ Auto-detect failed. Try manual identification with a TMDB ID.";
            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, result);
        }
        catch (Exception ex)
        {
            Log.Error("Error auto re-identifying collection", ex);
            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, $"❌ Error: {ex.Message}");
        }
    }

    private async Task RunManual(Message message, string collectionName)
    {
        var text = $"✏️ <b>Manual Re-identify:</b> {collectionName}\n\n" +
                   "Enter the TMDB ID (find it at themoviedb.org):";

        await _bot.EditMessageText(message.Chat.Id, message.MessageId, text,
            Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Cancel", EditCollectionCallback.Pack(_collectionId)) }
            }));

        await _pendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            Id,
            message.Chat.Id,
            message.MessageId,
            async (input) =>
            {
                _pendingActionHandler.Clear();
                if (!int.TryParse(input.Trim(), out int newTmdbId))
                {
                    await _bot.SendMessage(message.Chat.Id, "⚠️ Invalid TMDB ID. Must be a number.");
                    return;
                }

                var progress = await _bot.SendMessage(message.Chat.Id,
                    "⏳ Re-identifying collection… Please wait…");
                try
                {
                    var success = await _identifier.ReidentifyAsync(_collectionId, new CatalogIdentifier.Request(newTmdbId));
                    var result = success
                        ? "✅ Collection successfully re-identified!"
                        : "❌ Failed to re-identify. Make sure the TMDB ID is valid.";
                    await _bot.EditMessageText(message.Chat.Id, progress.MessageId, result);
                }
                catch (Exception ex)
                {
                    Log.Error("Error re-identifying collection", ex);
                    await _bot.EditMessageText(message.Chat.Id, progress.MessageId, $"❌ Error: {ex.Message}");
                }
            },
            async () =>
            {
                _pendingActionHandler.Clear();
                await new EditCollectionCallback(_collectionId, _bot, _catalog).ExecuteAsync(message);
            }
        ));
    }

    public static string Pack(int collectionId, string? subAction = null)
    {
        return subAction != null
            ? CallbackDataPacker.Pack(Id, [collectionId.ToString(), subAction])
            : CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var collectionId = int.Parse(fields[0]);
        var subAction = fields.Length > 1 ? fields[1] : null;
        return new ReidentifyCollectionCallback(
            collectionId,
            dispatcher.Bot,
            dispatcher.Catalog,
            dispatcher.Identifier,
            dispatcher.PendingActionHandler,
            subAction);
    }
}
