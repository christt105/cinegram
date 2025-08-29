using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

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

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new EditMovieCallback(int.Parse(fields[0]), botBot, botApiClient);
    }

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }
}