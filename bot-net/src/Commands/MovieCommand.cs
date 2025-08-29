using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
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

        var coverMessage = await _bot.SendPhoto(msg.Chat.Id,
            $"https://image.tmdb.org/t/p/w500{movie.PosterPath}",
            $"🎬 {movie.Title} ({movie.ReleaseYear}) [tmdbid-{movie.TmdbId}]");

        var infoText = $@"{movie.Title} ({movie.ReleaseYear})

ID: {movie.Id}
TMDB ID: [{movie.TmdbId}](https://www.themoviedb.org/movie/{movie.TmdbId})
Collections: {movie.Collections!.Length}";

        var buttons = new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📂 Collections") },
            new[] { InlineKeyboardButton.WithCallbackData("⬇️ Download") },
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Edit Movie") }
        };

        var infoMessage = await _bot.SendMessage(
            msg.Chat.Id,
            infoText,
            ParseMode.MarkdownV2,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public string Key => "/movie";
    public string Description => "Get a movie from the database.";
    public string Usage => "/movie <movie-id> | tmdbid-<movie-tmdb-id>";
}