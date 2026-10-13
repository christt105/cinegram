using Telegram.Bot.Types;
using Cinegram.Data;
using Cinegram.Jellyfin;
using Cinegram.TelegramBot.Handlers;
using Cinegram.Watch;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Watch;

/// <summary>
/// Arms a pending action expecting a reply of the form "tmdb &lt;id&gt;" (optionally
/// " season &lt;n&gt; episode &lt;n&gt;"), then runs the same resolve+move flow as
/// <see cref="ConfirmWatchedFileCallback"/>, recording the row as corrected.
/// </summary>
[Callback(Id)]
public class CorrectWatchedFileCallback : ICallbackQuery
{
    public const string Id = "correct-watched-file";

    private readonly WTelegram.Bot _bot;
    private readonly WatchedFiles _watchedFiles;
    private readonly WatchedFileMessageRegistry _registry;
    private readonly PendingActionHandler _pendingActionHandler;
    private readonly JellyfinSeriesIdentifier? _jellyfin;
    private readonly int _watchedFileId;

    private CorrectWatchedFileCallback(
        int watchedFileId, WTelegram.Bot bot, WatchedFiles watchedFiles,
        WatchedFileMessageRegistry registry, PendingActionHandler pendingActionHandler,
        JellyfinSeriesIdentifier? jellyfin)
    {
        _watchedFileId = watchedFileId;
        _bot = bot;
        _watchedFiles = watchedFiles;
        _registry = registry;
        _pendingActionHandler = pendingActionHandler;
        _jellyfin = jellyfin;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var filename = WatchedFileMessages.ExtractFilenameFromNotifyText(message!.Text);

        await _bot.EditMessageText(message.Chat.Id, message.MessageId,
            WatchedFileMessages.BuildCorrectionPromptText(filename));

        await ArmPendingAction(message, filename);
    }

    /// Re-armed on every invalid reply too, so a typo doesn't dead-end the conversation with no
    /// way to retry other than pressing "Correct" again from scratch.
    private Task ArmPendingAction(Message message, string filename) =>
        _pendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            id: $"correct-watched-file-{_watchedFileId}",
            chatId: message.Chat.Id,
            owner: message.MessageId,
            callback: async text => await HandleReply(text, message, filename),
            cancelCallback: async () =>
            {
                await _bot.EditMessageText(message.Chat.Id, message.MessageId, "Correction cancelled.");
            }
        ));

    private async Task HandleReply(string text, Message message, string filename)
    {
        if (!WatchedFileMessages.TryParseCorrection(text, out var parsed))
        {
            await _bot.EditMessageText(message.Chat.Id, message.MessageId,
                WatchedFileMessages.BuildCorrectionInvalidText());
            await ArmPendingAction(message, filename);
            return;
        }

        var outcome = await _watchedFiles.ResolveAsync(
            _watchedFileId, parsed.TmdbId, parsed.Season, parsed.Episode, "corrected");
        await WatchedFileMoveFlow.ExecuteAsync(
            _bot, _watchedFiles, _registry, message, _watchedFileId, outcome.Resolution, _jellyfin);
    }

    public static string Pack(int watchedFileId) =>
        CallbackDataPacker.Pack(Id, [watchedFileId.ToString()]);

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CorrectWatchedFileCallback(
            int.Parse(fields[0]), dispatcher.Bot, dispatcher.WatchedFiles,
            dispatcher.WatchedFileMessages, dispatcher.PendingActionHandler, dispatcher.JellyfinSeries);
    }
}
