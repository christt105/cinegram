using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Cinegram.Data;
using Cinegram.Library;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class ShowSeriesCallback : ICallbackQuery
{
    public const string Id = "Series";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;

    private ShowSeriesCallback(int seriesId, WTelegram.Bot bot, Catalog catalog)
    {
        _seriesId = seriesId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _catalog.GetSeriesAsync(_seriesId);

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

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot bot, Catalog catalog)
    {
        return new ShowSeriesCallback(int.Parse(fields[0]), bot, catalog);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
