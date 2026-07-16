using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class ShowSeriesCallback : ICallbackQuery
{
    public const string Id = "Series";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;

    private ShowSeriesCallback(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _seriesId = seriesId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _apiClient.GetSeriesAsync(_seriesId);

        var text = series != null ? Beautify.FormatSeries(series) : $"Series with ID: {_seriesId} not found";

        if (series?.PosterPath != null)
        {
            await _bot.DeleteMessages(message!.Chat.Id, new[] { message.MessageId });
            await _bot.SendPhoto(message.Chat.Id, MessageBuilder.FormatTmdbImageUrl(series.PosterPath), $"🎬 {series.ManualTitle} ({series.ReleaseYear}) [tmdbid-{series.TmdbId}]");
            await _bot.SendMessage(message.Chat.Id, text,
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: MessageBuilder.GetSeriesButtons(_seriesId));
        }
        else
        {
            await _bot.EditMessageText(
                message!.Chat.Id,
                message.MessageId,
                text,
                ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: MessageBuilder.GetSeriesButtons(_seriesId));
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ShowSeriesCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
