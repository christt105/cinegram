using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class SeeSeriesSeasonsCallback : ICallbackQuery
{
    public const string Id = "SeeSeriesSeasons";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;

    private SeeSeriesSeasonsCallback(int seriesId, WTelegram.Bot bot, ApiClient apiClient)
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
            await _bot.SendMessage(message!.Chat.Id, "No seasons found for this series.");
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var season in series.Seasons.OrderBy(s => s.SeasonNumber))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"Season {season.SeasonNumber} ({season.Episodes?.Length ?? 0} eps)",
                    SeeSeasonEpisodesCallback.Pack(_seriesId, season.Id))
            });
        }
        
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Back to Series", ShowSeriesCallback.Pack(_seriesId))
        });

        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            $"Seasons for <b>{series.ManualTitle}</b>:",
            Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new SeeSeriesSeasonsCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString()]);
    }
}
