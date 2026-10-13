using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;
using Cinegram.Data;
using Cinegram.Library;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Movie;

namespace Cinegram.TelegramBot.Commands;

public class MoviesCommand : ICommand
{
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;

    public MoviesCommand(WTelegram.Bot bot, Catalog catalog)
    {
        _bot = bot;
        _catalog = catalog;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var movies = await _catalog.ListMoviesAsync();

        if (movies == null || movies.Count == 0)
        {
            await _bot.SendMessage(msg.Chat.Id, "📭 No movies found.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var m in movies.Take(20))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"🎬 {m.Title} ({m.ReleaseYear})", Cinegram.TelegramBot.CallbackQueries.Callbacks.Movie.ShowMovieCallback.Pack(m.Id))
            });
        }

        var replyMarkup = new InlineKeyboardMarkup(buttons);
        var text = $"🎬 Movies (showing top {Math.Min(movies.Count, 20)}):";
        
        await _bot.SendMessage(msg.Chat.Id, text,
            replyMarkup: replyMarkup,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/movies";
    public string Description => "Lists all movies";
    public string Usage => "/movies";
}