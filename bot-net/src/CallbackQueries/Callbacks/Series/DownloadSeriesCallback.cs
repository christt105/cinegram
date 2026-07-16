using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class DownloadSeriesCallback : ICallbackQuery
{
    public const string Id = "DownloadSeries";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;

    private DownloadSeriesCallback(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _seriesId = seriesId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.SendMessage(message!.Chat.Id, "Próximamente: Descargar serie completa o por temporadas.");
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new DownloadSeriesCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
