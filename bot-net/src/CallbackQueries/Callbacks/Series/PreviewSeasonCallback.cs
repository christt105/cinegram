using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Series;

[Callback(Id)]
public class PreviewSeasonCallback : ICallbackQuery
{
    private const string Id = "PreviewSeason";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _seriesId;
    private readonly int _seasonId;

    public PreviewSeasonCallback(int seriesId, int seasonId, WTelegram.Bot bot, ApiClient apiClient)
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
            await _bot.SendMessage(message!.Chat.Id, $"Season with ID {_seasonId} not found.");
            return;
        }

        var allFiles = new List<Bot.Models.File>();

        // Add files from season-level collections
        if (season.Collections != null)
        {
            foreach (var c in season.Collections)
            {
                if (c.Files != null) allFiles.AddRange(c.Files);
            }
        }

        // Add files from episode-level collections
        if (season.Episodes != null)
        {
            foreach (var ep in season.Episodes)
            {
                if (ep.Collections != null)
                {
                    foreach (var c in ep.Collections)
                    {
                        if (c.Files != null) allFiles.AddRange(c.Files);
                    }
                }
            }
        }

        if (allFiles.Count == 0)
        {
            await _bot.SendMessage(message!.Chat.Id, "No files found for this season.");
            return;
        }

        // Gather quality info from all collections in the season
        var allCollections = new List<Bot.Models.Collection>();
        if (season.Collections != null) allCollections.AddRange(season.Collections);
        if (season.Episodes != null)
            foreach (var ep in season.Episodes)
                if (ep.Collections != null) allCollections.AddRange(ep.Collections);

        // Build header text
        var seriesTitle = $"\ud83d\udcfa {series.ManualTitle} ({series.ReleaseYear?.ToString() ?? "-"}) — Season {season.SeasonNumber}";
        if (series.TmdbId.HasValue)
            seriesTitle += $"  •  <a href=\"https://www.themoviedb.org/tv/{series.TmdbId}\">TMDB</a>";

        var text = $"<b>{seriesTitle}</b>\n\n";
        text += $"Found <b>{allFiles.Count}</b> file(s) for this season.";

        if (allCollections.Count > 0)
        {
            var qualityLines = allCollections
                .Select(c => Bot.Utils.Beautify.FormatCollectionQuality(c))
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Distinct()
                .ToList();

            if (qualityLines.Count > 0)
            {
                text += "\n\n" + string.Join("\n", qualityLines);
            }
        }

        if (series.PosterPath != null)
        {
            await _bot.SendPhoto(message!.Chat.Id,
                Bot.Utils.MessageBuilder.FormatTmdbImageUrl(series.PosterPath),
                text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
        }
        else
        {
            await _bot.SendMessage(message!.Chat.Id, text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
        }

        // Forward files using static helper
        var wtelegramFiles = await _bot.GetMessagesById(message!.Chat.Id, allFiles.Select(f => f.MessageId));
        await PreviewCollectionCallback.SendFilesAsGroup(_bot, message!.Chat.Id, wtelegramFiles);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new PreviewSeasonCallback(int.Parse(fields[0]), int.Parse(fields[1]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int seriesId, int seasonId)
    {
        return CallbackDataPacker.Pack(Id, [seriesId.ToString(), seasonId.ToString()]);
    }
}
