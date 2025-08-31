using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class EditMovieCallback : ICallbackQuery
{
    private EditMovieCallback(int movieId, WTelegram.Bot bot, ApiClient apiClient)
    {
        throw new NotImplementedException();
    }

    public const string Id = "edit-movie";

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new EditMovieCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public Task ExecuteAsync(Message? message)
    {
        throw new NotImplementedException();
    }
}