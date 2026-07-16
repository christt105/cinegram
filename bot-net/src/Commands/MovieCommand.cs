using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class MovieCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public MovieCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    private static string TmdbidPrefix => "tmdbid-";

    public async Task Execute(string[] args, Message msg)
    {
        if (args.Length == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "Please provide a Movie ID or a TMDB ID. Use `/help movie` to get more information.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var isDatabaseId = !args[0].StartsWith(TmdbidPrefix, StringComparison.CurrentCultureIgnoreCase);

        if (!int.TryParse(isDatabaseId ? args[0] : args[0][TmdbidPrefix.Length..], out var id))
        {
            Log.Error($"Failed to parse movie ID {args[0]}.");
            await _bot.SendMessage(msg.Chat.Id, "Please provide a valid ID.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var movie = isDatabaseId ? await _apiClient.GetMovieAsync(id) : await _apiClient.GetMovieByTmdbAsync(id);

        if (movie == null)
        {
            Log.Error($"Failed to get movie for {id}.");
            await _bot.SendMessage(msg.Chat.Id, $"Movie with ID {id} not found.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var coverMessage = movie.PosterPath != null
            ? await _bot.SendPhoto(msg.Chat.Id,
                MessageBuilder.FormatTmdbImageUrl(movie.PosterPath),
                Beautify.FormatMovieHeader(movie))
            : await _bot.SendMessage(msg.Chat.Id, Beautify.FormatMovieHeader(movie));

        var infoText = Beautify.FormatMovie(movie);

        var infoMessage = await _bot.SendMessage(
            msg.Chat.Id,
            infoText,
            ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: MessageBuilder.GetMovieButtons(movie.Id));
    }

    public string Key => "/movie";
    public string Description => "Get a movie from the database.";
    public string Usage => "/movie <movie-id> | tmdbid-<movie-tmdb-id>";
}