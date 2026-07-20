using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class SearchCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public SearchCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        if (args.Length == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "Please provide a search query. Usage: `/search <query>`",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var rawQuery = string.Join(" ", args);
        var query = TextNormalizer.Normalize(rawQuery);

        var movies = await _apiClient.GetMoviesAsync();
        var series = await _apiClient.GetSeriesAsync();

        var matchedMovies = movies?.Where(m => m.Title != null && TextNormalizer.Normalize(m.Title).Contains(query)).ToList() ?? new();
        var matchedSeries = series?.Where(s => s.ManualTitle != null && TextNormalizer.Normalize(s.ManualTitle).Contains(query)).ToList() ?? new();

        if (matchedMovies.Count == 0 && matchedSeries.Count == 0)
        {
            await _bot.SendMessage(msg.Chat.Id, $"📭 No results found for '{rawQuery}'.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var m in matchedMovies.Take(5))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"🎬 {m.Title} ({m.ReleaseYear})", Bot.CallbackQueries.Callbacks.Movie.ShowMovieCallback.Pack(m.Id))
            });
        }

        foreach (var s in matchedSeries.Take(5))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"📺 {s.ManualTitle} ({s.ReleaseYear})", Bot.CallbackQueries.Callbacks.Series.ShowSeriesCallback.Pack(s.Id))
            });
        }

        var keyboard = new InlineKeyboardMarkup(buttons);

        await _bot.SendMessage(
            msg.Chat.Id,
            $"🔎 Results for '{rawQuery}':",
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId }
        );
    }

    public string Key => "/search";
    public string Description => "Search movies and series";
    public string Usage => "/search <query>";
}
