using Bot.CallbackQueries.Callbacks.Movie;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class SelectCollectionToPreview : ICallbackQuery
{
    public const string Id = "selectCollectionToPreview";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _movieId;

    private SelectCollectionToPreview(int movieId, WTelegram.Bot bot, ApiClient apiClient)
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
            Log.Error($"Failed to get movie with ID {_movieId}");
            await _bot.SendMessage(message.Chat.Id, $"Failed to get the movie with ID {_movieId}");
            return;
        }

        if (movie.Collections == null || movie.Collections.Length == 0)
        {
            await _bot.SendMessage(message.Chat.Id, "Movie does not contain any valid collections");
            return;
        }

        if (movie.Collections.Length == 1)
        {
            // Only one collection just send
            await new PreviewCollectionCallback(movie.Collections[0].Id, _bot, _apiClient).ExecuteAsync(message);
            return;
        }

        var text = "Select a Collection to Preview:\n\n";

        var collections = movie.Collections;
        List<List<InlineKeyboardButton>> buttons = new();
        for (var i = 0; i < collections.Length; i++)
        {
            var c = collections[i];
            var qualityStr = string.IsNullOrWhiteSpace(c.Quality) ? "" : $" - Quality {c.Quality}";
            text += $"{i + 1}. \"{c.Name}\" (ID {c.Id}){qualityStr} - Files {c.Files!.Length}\n";
            buttons.Add([
                InlineKeyboardButton.WithCallbackData((i + 1).ToString(), PreviewCollectionCallback.Pack(c.Id))
            ]);
        }

        buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("Cancel", ShowMovieCallback.Pack(_movieId))
            ]
        );

        await _bot.EditMessageText(message.Chat.Id, message.MessageId, text, replyMarkup: buttons.ToArray());
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new SelectCollectionToPreview(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}