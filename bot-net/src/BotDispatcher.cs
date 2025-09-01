using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBot.Handlers;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;

namespace Bot;

public class BotDispatcher
{
    private readonly int _allowedUser;
    private readonly CallbackQueryHandler _callbackQueryHandler;
    private readonly CommandHandler _commandHandler;

    private readonly FileHandler _fileHandler;
    private readonly MessageHandler _messageHandler;
    private readonly PendingActionHandler _pendingActionHandler;

    public BotDispatcher(WTelegram.Bot bot, ApiClient apiClient, TaskQueue queue)
    {
        Bot = bot;
        ApiClient = apiClient;
        Queue = queue;

        _allowedUser = Convert.ToInt32(Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID"));

        _commandHandler = new CommandHandler(this);
        _fileHandler = new FileHandler(this);
        _messageHandler = new MessageHandler(Bot);
        _callbackQueryHandler = new CallbackQueryHandler(this);
        _pendingActionHandler = new PendingActionHandler(Bot);
    }

    public WTelegram.Bot Bot { get; }

    public ApiClient ApiClient { get; }

    public TaskQueue Queue { get; }

    public PendingActionHandler PendingActionHandler => _pendingActionHandler;

    public async Task InitBot()
    {
        var me = await Bot.GetMe();
        Log.Info($"Bot connected as @{me.Username}");

        await Bot.DropPendingUpdates();

        await Bot.SendMessage(_allowedUser, "Bot started");

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
                if (_pendingActionHandler.HasPendingAction())
                    await _pendingActionHandler.Handle(msg, type);
                else
                    await _messageHandler.Handle(msg, type);
        }
    }

    private async Task HandleUpdate(Update update)
    {
        switch (update.Type)
        {
            case UpdateType.CallbackQuery:
                if (update.CallbackQuery == null)
                {
                    Log.Error("Update type is CallbackQuery but no callback query was provided.");
                    return;
                }

                var callback = update.CallbackQuery;

                if (callback.From == null || callback.From.Id != _allowedUser)
                {
                    Log.Info($"User {callback.From?.Username} with ID({callback.From?.Id}) is not allowed.");
                    return;
                }

                await _callbackQueryHandler.HandleCallbackQueryAsync(callback);
                break;
            case UpdateType.Unknown:
                Console.WriteLine("Unknown update type: {0}", update.TLUpdate?.GetType().Name);
                break;
            default:
                Console.WriteLine($"No case to {update.Type}. {update.TLUpdate?.GetType().Name}");
                break;
        }
    }

    private Task HandleError(Exception e, HandleErrorSource src)
    {
        Log.Error($"Error ({src}) at {e.Source}", e);
        return Task.CompletedTask;
    }
}