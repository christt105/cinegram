using Bot.Handlers;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;

namespace Bot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class EditMovieCallback : ICallbackQuery
{
    public const string Id = "edit-movie";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;
    private readonly PendingActionHandler _pendingActionHandler;

    private EditMovieCallback(int movieId, WTelegram.Bot bot, ApiClient apiClient, PendingActionHandler pendingActionHandler)
    {
        _movieId = movieId;
        _bot = bot;
        _apiClient = apiClient;
        _pendingActionHandler = pendingActionHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var movie = await _apiClient.GetMovieAsync(_movieId);
        if (movie == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Movie not found.");
            return;
        }

        var text = $"✏️ <b>Edit Movie:</b> {movie.Title}\n\nWhat would you like to do?";

        var buttons = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔍 Re-identify Movie (Change TMDB ID)", CallbackDataPacker.Pack(Id, [_movieId.ToString(), "reidentify"])) },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Delete Movie", CallbackDataPacker.Pack(Id, [_movieId.ToString(), "delete"])) },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Back", ShowMovieCallback.Pack(_movieId)) }
        };

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        if (fields.Length > 1)
        {
            var movieId = int.Parse(fields[0]);
            var action = fields[1];
            if (action == "reidentify")
            {
                return new ReidentifyAction(movieId, dispatcher.Bot, dispatcher.ApiClient, dispatcher.PendingActionHandler);
            }
            if (action == "delete")
            {
                return new DeleteAction(movieId, dispatcher.Bot, dispatcher.ApiClient);
            }
            if (action == "confirm_delete")
            {
                return new ConfirmDeleteAction(movieId, dispatcher.Bot, dispatcher.ApiClient);
            }
        }
        return new EditMovieCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient, dispatcher.PendingActionHandler);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }

    private class ReidentifyAction : ICallbackQuery
    {
        private readonly int _movieId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;
        private readonly PendingActionHandler _pendingActionHandler;

        public ReidentifyAction(int movieId, WTelegram.Bot bot, ApiClient apiClient, PendingActionHandler pendingActionHandler)
        {
            _movieId = movieId;
            _bot = bot;
            _apiClient = apiClient;
            _pendingActionHandler = pendingActionHandler;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var movie = await _apiClient.GetMovieAsync(_movieId);
            if (movie == null)
            {
                await _bot.SendMessage(message!.Chat.Id, "Movie not found.");
                return;
            }

            var text = $"🔍 <b>Re-identify Movie:</b> {movie.Title}\n\nPlease enter the new TMDB ID (e.g. from themoviedb.org):";

            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("Cancel", EditMovieCallback.Pack(_movieId)) }));

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

                    var progress = await _bot.SendMessage(message.Chat.Id, "⏳ Re-identifying movie... Please wait...");
                    try
                    {
                        var success = await _apiClient.ReidentifyMovieAsync(_movieId, newTmdbId);
                        if (success)
                        {
                            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, "✅ Movie successfully re-identified! All files and collections have been moved to the new TMDB ID metadata.");
                        }
                        else
                        {
                            await _bot.EditMessageText(message.Chat.Id, progress.MessageId, "❌ Failed to re-identify movie. Please make sure the TMDB ID is valid and exists as a Movie.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error re-identifying movie", ex);
                        await _bot.EditMessageText(message.Chat.Id, progress.MessageId, $"❌ Error: {ex.Message}");
                    }
                },
                async () =>
                {
                    _pendingActionHandler.Clear();
                    await new EditMovieCallback(_movieId, _bot, _apiClient, _pendingActionHandler).ExecuteAsync(message);
                }
            ));
        }
    }

    private class DeleteAction : ICallbackQuery
    {
        private readonly int _movieId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;

        public DeleteAction(int movieId, WTelegram.Bot bot, ApiClient apiClient)
        {
            _movieId = movieId;
            _bot = bot;
            _apiClient = apiClient;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var movie = await _apiClient.GetMovieAsync(_movieId);
            if (movie == null)
            {
                await _bot.SendMessage(message!.Chat.Id, "Movie not found.");
                return;
            }

            var text = $"⚠️ <b>WARNING:</b> You are about to delete the movie <b>{movie.Title}</b>.\n\n" +
                       "This will unlink all its collections. The actual files will remain but will become uncategorized.\n\n" +
                       "Are you sure you want to proceed?";

            var buttons = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🔥 Confirm Delete", CallbackDataPacker.Pack(Id, [_movieId.ToString(), "confirm_delete"])) },
                new[] { InlineKeyboardButton.WithCallbackData("Cancel", EditMovieCallback.Pack(_movieId)) }
            };

            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }
    }

    private class ConfirmDeleteAction : ICallbackQuery
    {
        private readonly int _movieId;
        private readonly WTelegram.Bot _bot;
        private readonly ApiClient _apiClient;

        public ConfirmDeleteAction(int movieId, WTelegram.Bot bot, ApiClient apiClient)
        {
            _movieId = movieId;
            _bot = bot;
            _apiClient = apiClient;
        }

        public async Task ExecuteAsync(Message? message)
        {
            var success = await _apiClient.DeleteMovieAsync(_movieId);
            if (success)
            {
                await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "✅ Movie deleted successfully.");
            }
            else
            {
                await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "❌ Failed to delete movie.");
            }
        }
    }
}