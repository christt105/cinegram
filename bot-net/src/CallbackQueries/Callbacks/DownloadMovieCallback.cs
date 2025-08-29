using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

public class DownloadMovieCallback : ICallbackQuery
{
    private DownloadMovieCallback(int movieId, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        throw new NotImplementedException();
    }

    public const string Id = "download-movie";

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot botBot, ApiClient botApiClient)
    {
        return new DownloadMovieCallback(int.Parse(fields[0]), botBot, botApiClient);
    }
}