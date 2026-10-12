namespace Bot.Services;

/// <summary>
/// Singleton that exposes the live WTelegram.Bot and ApiClient instances
/// so that HTTP-triggered services (e.g. PreviewService) can access them
/// without tight coupling to the Worker.
/// </summary>
public class BotHolder
{
    private WTelegram.Bot? _bot;
    private ApiClient? _apiClient;
    private long _chatId;
    private WatchedFolderService? _watchedFolder;
    private WatchNotificationService? _watchNotifications;

    public void Register(WTelegram.Bot bot, ApiClient apiClient, long chatId)
    {
        _bot = bot;
        _apiClient = apiClient;
        _chatId = chatId;
    }

    public void RegisterWatch(WatchedFolderService watchedFolder, WatchNotificationService watchNotifications)
    {
        _watchedFolder = watchedFolder;
        _watchNotifications = watchNotifications;
    }

    public bool IsReady => _bot != null && _apiClient != null;

    public WTelegram.Bot Bot => _bot ?? throw new InvalidOperationException("Bot not yet initialised.");

    public ApiClient ApiClient => _apiClient ?? throw new InvalidOperationException("ApiClient not yet initialised.");

    public long ChatId => _chatId;

    public WatchedFolderService WatchedFolder =>
        _watchedFolder ?? throw new InvalidOperationException("WatchedFolderService not yet initialised.");

    /// <summary>Null until the bot is up; rows resolved before that are moved by
    /// <see cref="WatchNotificationService.ResumeResolvedAsync"/> on startup.</summary>
    public WatchNotificationService? WatchNotifications => _watchNotifications;
}
