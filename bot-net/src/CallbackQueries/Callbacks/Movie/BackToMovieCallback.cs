using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class BackToMovieCallback : ICallbackQuery
{
    public const string Id = "back-to-movie";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;

    private BackToMovieCallback(int movieId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _movieId = movieId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        var movie = await _apiClient.GetMovieAsync(_movieId);

        var text = movie != null ? Beautify.FormatMovie(movie) : $"Movie with ID: {_movieId} not found";

        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            text,
            ParseMode.MarkdownV2,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: MessageBuilder.GetMovieButtons(_movieId));
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new BackToMovieCallback(int.Parse(fields[0]), botBot, botApiClient);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}