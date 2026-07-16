using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class ShowEpisodeCallback : ICallbackQuery
{
    public const string Id = "ShowEp";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _seriesId;
    private readonly int _seasonId;
    private readonly int _episodeId;

    private ShowEpisodeCallback(int seriesId, int seasonId, int episodeId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _seriesId = seriesId;
        _seasonId = seasonId;
        _episodeId = episodeId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var series = await _apiClient.GetSeriesAsync(_seriesId);
        if (series == null || series.Seasons == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Series not found.");
            return;
        }

        var season = series.Seasons.FirstOrDefault(s => s.Id == _seasonId);
        if (season == null || season.Episodes == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Season not found.");
            return;
        }

        var episode = season.Episodes.FirstOrDefault(e => e.Id == _episodeId);
        if (episode == null)
        {
            await _bot.SendMessage(message!.Chat.Id, "Episode not found.");
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();

        var text = $"""
                    <b>Series:</b> {series.ManualTitle}
                    <b>Season:</b> {season.SeasonNumber}
                    <b>Episode:</b> {episode.EpisodeNumber}
                    <b>Title:</b> {episode.Title ?? "Unknown"}
                    
                    <b>Collections:</b>
                    
                    """;

        if (episode.Collections != null && episode.Collections.Length > 0)
        {
            for (var i = 0; i < episode.Collections.Length; i++)
            {
                var c = episode.Collections[i];
                text += $"{i + 1}. \"{c.Name}\" - Quality {c.Quality}\n";
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"📥 Download #{i + 1}", Bot.CallbackQueries.Callbacks.Collection.ShowCollectionCallback.Pack(c.Id))
                });
            }
        }
        else
        {
            text += "No collections available.\n";
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Back to Episodes", SeeSeasonEpisodesCallback.Pack(_seriesId, _seasonId))
        });

        await _bot.EditMessageText(
            message!.Chat.Id,
            message.MessageId,
            text,
            Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ShowEpisodeCallback(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId, int seasonId, int episodeId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString(), seasonId.ToString(), episodeId.ToString()]);
    }
}
