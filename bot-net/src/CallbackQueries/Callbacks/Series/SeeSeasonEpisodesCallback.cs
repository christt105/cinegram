using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class SeeSeasonEpisodesCallback : ICallbackQuery
{
    public const string Id = "SeasonEps";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;
    private readonly int _seasonId;

    private SeeSeasonEpisodesCallback(int seriesId, int seasonId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _seriesId = seriesId;
        _seasonId = seasonId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _apiClient.GetSeriesAsync(_seriesId);
        if (series == null || series.Seasons == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Series or seasons not found.");
            return;
        }

        var season = series.Seasons.FirstOrDefault(s => s.Id == _seasonId);
        
        if (season == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Season not found.");
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var episode in season.Episodes.OrderBy(e => e.EpisodeNumber))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"Ep {episode.EpisodeNumber}: {episode.Title ?? "Unknown"}",
                    ShowEpisodeCallback.Pack(_seriesId, _seasonId, episode.Id))
            });
        }
        
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Back to Seasons", SeeSeriesSeasonsCallback.Pack(_seriesId))
        });

        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            $"Episodes for <b>{series.ManualTitle}</b> - Season {season.SeasonNumber}:",
            Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new SeeSeasonEpisodesCallback(int.Parse(fields[0]), int.Parse(fields[1]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId, int seasonId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString(), seasonId.ToString()]);
    }
}
