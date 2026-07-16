using Bot.Handlers;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class EditSeriesCallback : ICallbackQuery
{
    public const string Id = "EditSeries";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;
    private readonly PendingActionHandler _pendingActionHandler;

    private EditSeriesCallback(int seriesId, WTelegram.Bot bot, ApiClient apiClient, PendingActionHandler pendingActionHandler)
    {
        _seriesId = seriesId;
        _bot = bot;
        _apiClient = apiClient;
        _pendingActionHandler = pendingActionHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _apiClient.GetSeriesAsync(_seriesId);
        if (series == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Series not found.");
            return;
        }

        var text = $"✏️ <b>Edit Series:</b> {series.ManualTitle}\n\nWhat would you like to do?";

        var buttons = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔍 Re-identify Series (Change TMDB ID)", CallbackDataPacker.Pack(Id, [_seriesId.ToString(), "reidentify"])) },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Delete Series", CallbackDataPacker.Pack(Id, [_seriesId.ToString(), "delete"])) },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Back", ShowSeriesCallback.Pack(_seriesId)) }
        };

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        if (fields.Length > 1)
        {
            var seriesId = int.Parse(fields[0]);
            var action = fields[1];
            if (action == "reidentify")
            {
                return new ReidentifyAction(seriesId, dispatcher.Bot, dispatcher.ApiClient, dispatcher.PendingActionHandler);
            }
            if (action == "delete")
            {
                return new DeleteAction(seriesId, dispatcher.Bot, dispatcher.ApiClient);
            }
            if (action == "confirm_delete")
            {
                return new ConfirmDeleteAction(seriesId, dispatcher.Bot, dispatcher.ApiClient);
            }
        }
        return new EditSeriesCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient, dispatcher.PendingActionHandler);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }

    private class ReidentifyAction : ICallbackQuery
    {
        private readonly int _seriesId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;
        private readonly PendingActionHandler _pendingActionHandler;

        public ReidentifyAction(int seriesId, WTelegram.Bot bot, ApiClient apiClient, PendingActionHandler pendingActionHandler)
        {
            _seriesId = seriesId;
            _bot = bot;
            _apiClient = apiClient;
            _pendingActionHandler = pendingActionHandler;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var series = await _apiClient.GetSeriesAsync(_seriesId);
            if (series == null)
            {
                await _bot.SendMessage(message!.Chat.Id, "Series not found.");
                return;
            }

            var text = $"🔍 <b>Re-identify Series:</b> {series.ManualTitle}\n\nPlease enter the new TMDB ID (e.g. from themoviedb.org):";

            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("Cancel", EditSeriesCallback.Pack(_seriesId)) }));

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

                    var progress = await _bot.SendMessage(message.Chat.Id, "⏳ Re-identifying series and re-mapping all episodes. Please wait...");
                    try
                    {
                        var success = await _apiClient.ReidentifySeriesAsync(_seriesId, newTmdbId);
                        if (success)
                        {
                            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, "✅ Series successfully re-identified! All files and collections have been moved to the new TMDB ID metadata.");
                        }
                        else
                        {
                            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, "❌ Failed to re-identify series. Please make sure the TMDB ID is valid and exists as a TV show.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error re-identifying series", ex);
                        await _bot.EditMessageText(message.Chat.Id, progress.MessageId, $"❌ Error: {ex.Message}");
                    }
                },
                async () =>
                {
                    _pendingActionHandler.Clear();
                    await new EditSeriesCallback(_seriesId, _bot, _apiClient, _pendingActionHandler).ExecuteAsync(message);
                }
            ));
        }
    }

    private class DeleteAction : ICallbackQuery
    {
        private readonly int _seriesId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;

        public DeleteAction(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
        {
            _seriesId = seriesId;
            _bot = bot;
            _apiClient = apiClient;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var series = await _apiClient.GetSeriesAsync(_seriesId);
            if (series == null)
            {
                await _bot.SendMessage(message!.Chat.Id, "Series not found.");
                return;
            }

            var text = $"⚠️ <b>WARNING:</b> You are about to delete the series <b>{series.ManualTitle}</b>.\n\n" +
                       "This will unlink all its collections, seasons, and episodes. The actual files will remain but will become uncategorized.\n\n" +
                       "Are you sure you want to proceed?";

            var buttons = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🔥 Confirm Delete", CallbackDataPacker.Pack(Id, [_seriesId.ToString(), "confirm_delete"])) },
                new[] { InlineKeyboardButton.WithCallbackData("Cancel", EditSeriesCallback.Pack(_seriesId)) }
            };

            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }
    }

    private class ConfirmDeleteAction : ICallbackQuery
    {
        private readonly int _seriesId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;

        public ConfirmDeleteAction(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
        {
            _seriesId = seriesId;
            _bot = bot;
            _apiClient = apiClient;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var success = await _apiClient.DeleteSeriesAsync(_seriesId);
            if (success)
            {
                await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "✅ Series deleted successfully.");
            }
            else
            {
                await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "❌ Failed to delete series.");
            }
        }
    }
}
