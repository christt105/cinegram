using Telegram.Bot.Types;
using Cinegram.Data;
using Cinegram.Library;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class DownloadSeriesCallback : ICallbackQuery
{
    public const string Id = "DownloadSeries";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;

    private DownloadSeriesCallback(int seriesId, WTelegram.Bot bot, Catalog catalog)
    {
        _seriesId = seriesId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.SendMessage(message!.Chat.Id, "Coming soon: Download full series or by season.");
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new DownloadSeriesCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
