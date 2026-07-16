using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class ShowMovieCallback : ICallbackQuery
{
    public const string Id = "Movie";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;

    private ShowMovieCallback(int movieId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _movieId = movieId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var movie = await _apiClient.GetMovieAsync(_movieId);

        var text = movie != null ? Beautify.FormatMovie(movie) : $"Movie with ID: {_movieId} not found";

        if (movie?.PosterPath != null)
        {
            await _bot.DeleteMessages(message!.Chat.Id, new[] { message.MessageId });
            await _bot.SendPhoto(message.Chat.Id, MessageBuilder.FormatTmdbImageUrl(movie.PosterPath), Beautify.FormatMovieHeader(movie));
            await _bot.SendMessage(message.Chat.Id, text,
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: MessageBuilder.GetMovieButtons(_movieId));
        }
        else
        {
            await _bot.EditMessageText(
                message!.Chat.Id,
                message.MessageId,
                text,
                ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: MessageBuilder.GetMovieButtons(_movieId));
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ShowMovieCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}