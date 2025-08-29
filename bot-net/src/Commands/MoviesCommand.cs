using Bot.Services;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class MoviesCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public MoviesCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var movies = await _apiClient.GetMoviesAsync();

        var message = "📭 No movies found.";
        if (movies != null && movies.Count > 0)
        {
            message = "🎬 Movies:\n";
            for (var i = 0; i < Math.Min(movies.Count, 10); i++)
            {
                var movie = movies[i];
                message += $"- {movie.Id}: {movie.Title} ({movie.ReleaseYear}) [tmdbid-{movie.TmdbId}]\n";
            }

            if (movies.Count > 10) message += $"\n... and {movies.Count - 10} more";
        }

        await _bot.SendMessage(msg.Chat.Id, message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/movies";
    public string Description => "Lists all movies";
    public string Usage => "/movies";
}