
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WTelegram.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Handlers;

public class PendingActionHandler
{
    public class PendingAction
    {
        public PendingAction(string id, ChatId chatId, object? owner = null, Func<string, Task>? callback = null, Func<Task>? cancelCallback = null)
        {
            Id = id;
            ChatId = chatId;
            Owner = owner;
            Callback = callback;
            CancelCallback = cancelCallback;
        }
        public string Id { get; set; }
        public ChatId ChatId { get; set; }
        public object? Owner { get; set; }
        public Func<string, Task>? Callback { get; set; }
        public Func<Task>? CancelCallback { get; set; }
    }

    private WTelegram.Bot _bot;

    private PendingAction? _currentAction;

    public PendingAction? CurrentAction => _currentAction;

    public PendingActionHandler(WTelegram.Bot bot)
    {
        _bot = bot;
    }

    public async Task SetPendingAction(PendingAction action)
    {
        if (_currentAction != null)
        {
            if (action.Owner != null && _currentAction.Owner != null && action.Owner.Equals(_currentAction.Owner))
            {
                Log.Info("Trying to set the same pending action again, ignoring");
                return;
            }
            Log.Info("Overwriting existing pending action");
            await _bot.SendMessage(_currentAction.ChatId, "Previous action cancelled");
            if (_currentAction.CancelCallback != null)
                await _currentAction.CancelCallback.Invoke();
            
            Clear();
        }

        _currentAction = action;
    }

    internal async Task Handle(Message? msg, UpdateType type)
    {
        if (msg == null)
            return;
        if (_currentAction == null)
            return;

        if (string.IsNullOrEmpty(msg.Text))
            return;

        await _currentAction.Callback?.Invoke(msg.Text)!;
        
        Clear();
    }

    internal bool HasPendingAction()
    {
        return _currentAction != null;
    }

    public void Clear()
    {
        _currentAction = null;
    }
}