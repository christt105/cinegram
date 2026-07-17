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

    public void Register(WTelegram.Bot bot, ApiClient apiClient, long chatId)
    {
        _bot = bot;
        _apiClient = apiClient;
        _chatId = chatId;
    }

    public bool IsReady => _bot != null && _apiClient != null;

    public WTelegram.Bot Bot => _bot ?? throw new InvalidOperationException("Bot not yet initialised.");

    public ApiClient ApiClient => _apiClient ?? throw new InvalidOperationException("ApiClient not yet initialised.");

    public long ChatId => _chatId;
}
