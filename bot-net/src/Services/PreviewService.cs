using Bot.CallbackQueries.Callbacks.Collection;
using Bot.CallbackQueries.Callbacks.Series;
using Bot.Utils;
using Telegram.Bot.Types.Enums;

namespace Bot.Services;

/// <summary>
/// Sends Telegram preview messages (info card + actual files) for a collection or a season,
/// triggered by the internal HTTP API rather than by a Telegram callback.
/// </summary>
public class PreviewService
{
    private readonly BotHolder _holder;

    public PreviewService(BotHolder holder)
    {
        _holder = holder;
    }

    /// <summary>
    /// Sends the info card and all files for a single movie collection to the owner's chat.
    /// </summary>
    public async Task<(bool ok, string error)> SendCollectionPreviewAsync(int collectionId)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.");

        var bot = _holder.Bot;
        var apiClient = _holder.ApiClient;
        var chatId = _holder.ChatId;

        var collection = await apiClient.GetCollectionAsync(collectionId);
        if (collection is null)
            return (false, $"Collection {collectionId} not found.");

        if (collection.Files == null || collection.Files.Length == 0)
            return (false, "Collection is empty.");

        // Build and send the info card
        if (collection.MovieId != null)
        {
            var movie = await apiClient.GetMovieAsync(collection.MovieId.Value);
            if (movie != null)
            {
                var movieTitle = $"\ud83c\udfac {movie.Title} ({movie.ReleaseYear})";
                if (movie.TmdbId.HasValue)
                    movieTitle += $"  \u2022  <a href=\"https://www.themoviedb.org/movie/{movie.TmdbId}\">TMDB</a>";

                var text = Beautify.FormatCollectionPreviewHeader(collection, movieTitle);

                if (movie.PosterPath != null)
                    await bot.SendPhoto(chatId, MessageBuilder.FormatTmdbImageUrl(movie.PosterPath),
                        text, parseMode: ParseMode.Html);
                else
                    await bot.SendMessage(chatId, text, ParseMode.Html);
            }
        }

        // Forward the actual files
        var messages = await bot.GetMessagesById(chatId, collection.Files.Select(f => f.MessageId));
        await PreviewCollectionCallback.SendFilesAsGroup(bot, chatId, messages);

        return (true, string.Empty);
    }

    /// <summary>
    /// Sends the info card and all files for every collection in a season to the owner's chat.
    /// </summary>
    public async Task<(bool ok, string error)> SendSeasonPreviewAsync(int seriesId, int seasonNumber)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.");

        var bot = _holder.Bot;
        var apiClient = _holder.ApiClient;
        var chatId = _holder.ChatId;

        var series = await apiClient.GetSeriesAsync(seriesId);
        if (series == null || series.Seasons == null)
            return (false, "Series or seasons not found.");

        var season = series.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
        if (season == null)
            return (false, $"Season {seasonNumber} not found.");

        // Gather all files across season packs and episode collections
        var allFiles = new List<Bot.Models.File>();
        if (season.Collections != null)
            foreach (var c in season.Collections)
                if (c.Files != null) allFiles.AddRange(c.Files);

        if (season.Episodes != null)
            foreach (var ep in season.Episodes)
                if (ep.Collections != null)
                    foreach (var c in ep.Collections)
                        if (c.Files != null) allFiles.AddRange(c.Files);

        if (allFiles.Count == 0)
            return (false, "No files found for this season.");

        // Build and send the info card
        var allCollections = new List<Bot.Models.Collection>();
        if (season.Collections != null) allCollections.AddRange(season.Collections);
        if (season.Episodes != null)
            foreach (var ep in season.Episodes)
                if (ep.Collections != null) allCollections.AddRange(ep.Collections);

        var seriesTitle = $"\ud83d\udcfa {series.ManualTitle} ({series.ReleaseYear?.ToString() ?? "-"}) \u2014 Season {season.SeasonNumber}";
        if (series.TmdbId.HasValue)
            seriesTitle += $"  \u2022  <a href=\"https://www.themoviedb.org/tv/{series.TmdbId}\">TMDB</a>";

        var text = $"<b>{seriesTitle}</b>\n\n";
        text += $"Found <b>{allFiles.Count}</b> file(s) for this season.";

        var qualityLines = allCollections
            .Select(c => Beautify.FormatCollectionQuality(c))
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct()
            .ToList();

        if (qualityLines.Count > 0)
            text += "\n\n" + string.Join("\n", qualityLines);

        if (series.PosterPath != null)
            await bot.SendPhoto(chatId, MessageBuilder.FormatTmdbImageUrl(series.PosterPath),
                text, parseMode: ParseMode.Html);
        else
            await bot.SendMessage(chatId, text, ParseMode.Html);

        // Forward all files
        var wtFiles = await bot.GetMessagesById(chatId, allFiles.Select(f => f.MessageId));
        await PreviewCollectionCallback.SendFilesAsGroup(bot, chatId, wtFiles);

        return (true, string.Empty);
    }
}
