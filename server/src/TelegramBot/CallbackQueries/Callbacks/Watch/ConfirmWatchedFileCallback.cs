using Telegram.Bot.Types;
using Cinegram.Jellyfin;
using Cinegram.Watch;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Watch;

/// <summary>
/// Accepts the guess a notify message showed as-is. The guess (tmdb id/season/episode) travels
/// packed in the callback data itself, so the button acts on the guess the message showed.
/// </summary>
[Callback(Id)]
public class ConfirmWatchedFileCallback : ICallbackQuery
{
    public const string Id = "confirm-watched-file";

    private readonly WTelegram.Bot _bot;
    private readonly WatchedFiles _watchedFiles;
    private readonly WatchedFileMessageRegistry _registry;
    private readonly JellyfinSeriesIdentifier? _jellyfin;
    private readonly int _watchedFileId;
    private readonly int _tmdbId;
    private readonly int? _season;
    private readonly int? _episode;

    private ConfirmWatchedFileCallback(
        int watchedFileId, int tmdbId, int? season, int? episode,
        WTelegram.Bot bot, WatchedFiles watchedFiles, WatchedFileMessageRegistry registry,
        JellyfinSeriesIdentifier? jellyfin)
    {
        _jellyfin = jellyfin;
        _watchedFileId = watchedFileId;
        _tmdbId = tmdbId;
        _season = season;
        _episode = episode;
        _bot = bot;
        _watchedFiles = watchedFiles;
        _registry = registry;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var outcome = await _watchedFiles.ResolveAsync(_watchedFileId, _tmdbId, _season, _episode, "confirmed");
        await WatchedFileMoveFlow.ExecuteAsync(
            _bot, _watchedFiles, _registry, message!, _watchedFileId, outcome.Resolution, _jellyfin);
    }

    public static string Pack(int watchedFileId, int tmdbId, int? season, int? episode) =>
        CallbackDataPacker.Pack(Id,
            [watchedFileId.ToString(), tmdbId.ToString(), season?.ToString() ?? "", episode?.ToString() ?? ""]);

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var season = string.IsNullOrEmpty(fields[2]) ? (int?)null : int.Parse(fields[2]);
        var episode = string.IsNullOrEmpty(fields[3]) ? (int?)null : int.Parse(fields[3]);

        return new ConfirmWatchedFileCallback(
            int.Parse(fields[0]), int.Parse(fields[1]), season, episode,
            dispatcher.Bot, dispatcher.WatchedFiles, dispatcher.WatchedFileMessages, dispatcher.JellyfinSeries);
    }
}
