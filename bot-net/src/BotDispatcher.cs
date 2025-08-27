using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBot.Handlers;
using WTelegram.Types;

namespace Bot;

public class BotDispatcher
{
    private readonly int _allowedUser;
    private readonly CommandHandler _commandHandler;

    private readonly FileHandler _fileHandler;
    private readonly MessageHandler _messageHandler;

    public BotDispatcher(WTelegram.Bot bot, ApiClient apiClient)
    {
        Bot = bot;
        ApiClient = apiClient;

        _allowedUser = Convert.ToInt32(Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID"));

        _commandHandler = new CommandHandler(this);
        _fileHandler = new FileHandler(Bot);
        _messageHandler = new MessageHandler(Bot);
    }

    public WTelegram.Bot Bot { get; }

    public ApiClient ApiClient { get; }

    public async Task InitBot()
    {
        var me = await Bot.GetMe();
        Log.Info($"Bot connected as @{me.Username}");

        await Bot.DropPendingUpdates();

        Bot.OnMessage += HandleMessage;
        Bot.OnUpdate += HandleUpdate;
        Bot.OnError += HandleError;

        Log.Info("Bot initialized. Waiting for updates...");
    }

    public async Task HandleMessage(Message msg, UpdateType type)
    {
        if (msg.From == null || msg.From.Id != _allowedUser)
        {
            Log.Info($"User {msg.From?.Username} with ID({msg.From?.Id}) is not allowed.");
            return;
        }

        if (msg.Document != null) await _fileHandler.Handle(msg, type);

        if (!string.IsNullOrEmpty(msg.Text))
        {
            if (msg.Text.StartsWith('/'))
                await _commandHandler.Handle(msg, type);
            else
                await _messageHandler.Handle(msg, type);
        }
    }

    private Task HandleUpdate(Update update)
    {
        if (update.Type == UpdateType.Unknown)
            Console.WriteLine("Unknown update type: {0}", update.TLUpdate?.GetType().Name);

        Console.WriteLine(update.Type.ToString());

        return Task.CompletedTask;
    }

    private Task HandleError(Exception e, HandleErrorSource src)
    {
        Log.Error($"Error ({src}) at {e.Source}", e);
        return Task.CompletedTask;
    }
}