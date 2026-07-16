using Bot.CallbackQueries.Callbacks.Series;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class SelectSeasonToPreview : ICallbackQuery
{
    public const string Id = "selectSeasonToPreview";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _seriesId;

    private SelectSeasonToPreview(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _seriesId = seriesId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _apiClient.GetSeriesAsync(_seriesId);

        if (series == null || series.Seasons == null || series.Seasons.Length == 0)
        {
            await _bot.SendMessage(message!.Chat.Id, "Series does not contain any valid seasons");
            return;
        }

        if (series.Seasons.Length == 1)
        {
            // Only one season just send
            await new PreviewSeasonCallback(_seriesId, series.Seasons[0].Id, _bot, _apiClient).ExecuteAsync(message);
            return;
        }

        var text = "Select a Season to Preview:\n\n";

        var seasons = series.Seasons.OrderBy(s => s.SeasonNumber).ToArray();
        List<List<InlineKeyboardButton>> buttons = new();
        for (var i = 0; i < seasons.Length; i++)
        {
            var s = seasons[i];
            text += $"{i + 1}. \"Season {s.SeasonNumber}\" (ID {s.Id})\n";
            buttons.Add([
                InlineKeyboardButton.WithCallbackData($"Season {s.SeasonNumber}", PreviewSeasonCallback.Pack(_seriesId, s.Id))
            ]);
        }

        buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("\u2b05\ufe0f Back to series", ShowSeriesCallback.Pack(_seriesId))
            ]
        );

        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            text,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new SelectSeasonToPreview(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
