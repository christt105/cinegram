namespace Bot.Services;

/// <summary>
/// Singleton that exposes the live WTelegram.Bot instance
/// so that HTTP-triggered services (e.g. PreviewService) can access it
/// without tight coupling to the Worker.
/// </summary>
public class BotHolder
{
    private WTelegram.Bot? _bot;
    private long _chatId;
    private WatchedFolderService? _watchedFolder;
    private WatchNotificationService? _watchNotifications;

    public void Register(WTelegram.Bot bot, long chatId)
    {
        _bot = bot;
        _chatId = chatId;
    }

    public void RegisterWatch(WatchedFolderService watchedFolder, WatchNotificationService watchNotifications)
    {
        _watchedFolder = watchedFolder;
        _watchNotifications = watchNotifications;
    }

    public bool IsReady => _bot != null;

    public WTelegram.Bot Bot => _bot ?? throw new InvalidOperationException("Bot not yet initialised.");

    public long ChatId => _chatId;

    public WatchedFolderService WatchedFolder =>
        _watchedFolder ?? throw new InvalidOperationException("WatchedFolderService not yet initialised.");

    /// <summary>Null until the bot is up; rows resolved before that are moved by
    /// <see cref="WatchNotificationService.ResumeResolvedAsync"/> on startup.</summary>
    public WatchNotificationService? WatchNotifications => _watchNotifications;
}
