using System.Collections.Concurrent;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Jellyfin;
using Cinegram.TelegramBot;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Watch;
using Cinegram.MediaFiles;

namespace Cinegram.Watch;

/// <summary>
/// Telegram side of the watch flow. Sends the Confirm/Correct message as soon as a file is found,
/// edits a still-live message when its file disappears, and moves rows confirmed or corrected from
/// the web. <see cref="NotifyPendingAsync"/> and <see cref="ResumeResolvedAsync"/> catch up on rows
/// left behind by a restart or a failed send.
/// </summary>
public class WatchNotificationService
{
    private readonly WTelegram.Bot _bot;
    private readonly WatchedFiles _watchedFiles;
    private readonly WatchedFileMessageRegistry _registry;
    private readonly JellyfinSeriesIdentifier? _jellyfin;
    private readonly TelegramOptions _telegram;
    private readonly LibraryPaths _library;
    private readonly ConcurrentDictionary<int, byte> _notifying = new();

    public WatchNotificationService(
        WTelegram.Bot bot, WatchedFiles watchedFiles, WatchedFileMessageRegistry registry,
        TelegramOptions telegram, LibraryPaths library, JellyfinSeriesIdentifier? jellyfin = null)
    {
        _telegram = telegram;
        _library = library;
        _bot = bot;
        _watchedFiles = watchedFiles;
        _registry = registry;
        _jellyfin = jellyfin;
    }

    /// <summary>Sends the notify message for a row that is still pending; a row already being
    /// notified, or no longer pending, is skipped.</summary>
    public async Task NotifyAsync(int watchedFileId)
    {
        if (!_notifying.TryAdd(watchedFileId, 0)) return;

        try
        {
            var row = await _watchedFiles.GetAsync(watchedFileId);
            if (row is not { Status: "pending" }) return;

            var sent = await _bot.SendMessage(_telegram.OwnerUserId, BuildNotifyMessageText(row), ParseMode.Html,
                replyMarkup: BuildNotifyButtons(row));
            _registry.Track(row.Id, sent.Chat.Id, sent.MessageId);

            Log.Info($"[WatchNotification] Notified about {row.Filename} (row {row.Id}).");
            await _watchedFiles.MarkNotifiedAsync(row.Id);
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchNotification] Failed to notify row {watchedFileId}", ex);
        }
        finally
        {
            _notifying.TryRemove(watchedFileId, out _);
        }
    }

    public async Task NotifyPendingAsync()
    {
        foreach (var row in await _watchedFiles.ListAsync("pending"))
            await NotifyAsync(row.Id);
    }

    /// <summary>Edits the row's live message, if any, so it doesn't keep offering buttons for a
    /// file that is gone.</summary>
    public async Task NotifyRemovedAsync(WatchedFile row)
    {
        if (row.Status != "removed" || !_registry.TryUntrack(row.Id, out var reference)) return;

        Log.Info($"[WatchNotification] {row.Filename} (row {row.Id}) was removed before it was confirmed.");
        try
        {
            await _bot.EditMessageText(
                reference.ChatId, reference.MessageId,
                WatchedFileMessages.BuildRemovedWhileNotifiedText(row.Filename));
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchNotification] Failed to edit the message of row {row.Id}", ex);
        }
    }

    /// <summary>
    /// Moves a row confirmed or corrected from the web, whether or not a notify message for it went
    /// out, and edits that message with the outcome if it is still live. A Telegram tap racing it
    /// for the same row is <see cref="WatchedFileMoveClaims"/>'s job: whichever claims the row first
    /// moves it and reports the outcome.
    /// </summary>
    public async Task MoveResolvedAsync(WatchedFileResolution resolution, CancellationToken ct = default)
    {
        try
        {
            Log.Info($"[WatchNotification] Moving {resolution.Filename} (row {resolution.Id}, {resolution.Status} from the web).");
            var outcome = await WatchedFileMoveFlow.MoveAndReportAsync(
                _watchedFiles, _library, resolution.Id, resolution, _jellyfin, ct);
            if (outcome is null)
            {
                Log.Info($"[WatchNotification] {resolution.Filename} (row {resolution.Id}) is already being moved, leaving it.");
                return;
            }

            Log.Info($"[WatchNotification] {resolution.Filename} (row {resolution.Id}): {outcome.Value.Text}");
            if (_registry.TryUntrack(resolution.Id, out var reference))
                await _bot.EditMessageText(reference.ChatId, reference.MessageId, outcome.Value.Text);
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchNotification] Failed to move row {resolution.Id}", ex);
        }
    }

    /// <summary>Moves rows left confirmed or corrected by a restart between settling their
    /// identity and moving them.</summary>
    public async Task ResumeResolvedAsync(CancellationToken ct)
    {
        foreach (var status in new[] { "confirmed", "corrected" })
        foreach (var row in await _watchedFiles.ListAsync(status, ct))
        {
            var resolution = WatchedFileResolution.FromWatchedFile(row);
            if (resolution is null)
            {
                Log.Error($"[WatchNotification] Row {row.Id} is {status} without a resolved guess, skipping.");
                continue;
            }

            await MoveResolvedAsync(resolution, ct);
        }
    }

    /// <summary>Also used by <see cref="CancelConfirmWatchedFileCallback"/> to restore the
    /// original notify message when the user backs out of the confirmation prompt.</summary>
    public static string BuildNotifyMessageText(WatchedFile row) =>
        WatchedFileMessages.BuildNotifyText(
            row.Filename, row.GuessMediaType, row.GuessTitle, row.GuessSeason, row.GuessEpisode, row.Confidence,
            row.GuessTmdbId);

    /// <summary>Also used by <see cref="CancelConfirmWatchedFileCallback"/> to restore the
    /// original notify message when the user backs out of the confirmation prompt.</summary>
    public static InlineKeyboardButton[][] BuildNotifyButtons(WatchedFile row)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (row.GuessTmdbId.HasValue)
        {
            rows.Add([
                InlineKeyboardButton.WithCallbackData(
                    "✅ Confirm",
                    AskConfirmWatchedFileCallback.Pack(row.Id, row.GuessTmdbId.Value, row.GuessSeason, row.GuessEpisode))
            ]);
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("✏️ Correct", CorrectWatchedFileCallback.Pack(row.Id))]);

        return rows.ToArray();
    }
}
