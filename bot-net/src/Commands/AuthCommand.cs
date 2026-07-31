using Bot.Handlers;
using Bot.Services;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class AuthCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly UserClientService _userClient;
    private readonly PendingActionHandler _pending;
    private readonly int _apiId;
    private readonly string _apiHash;

    public AuthCommand(WTelegram.Bot bot, UserClientService userClient, PendingActionHandler pending)
    {
        _bot = bot;
        _userClient = userClient;
        _pending = pending;
        _apiId = int.Parse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID")!);
        _apiHash = Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")!;
    }

    public string Key => "/auth";
    public string Description => "Authenticate with a Telegram user account for Premium transfers.";
    public string Usage => "/auth";

    public async Task Execute(string[] args, Message msg)
    {
        if (_userClient.IsAuthenticated)
        {
            await _bot.SendMessage(msg.Chat.Id,
                $"Already authenticated. Premium: {_userClient.IsPremium}\nRun /auth again to re-authenticate.");
        }

        await _bot.SendMessage(msg.Chat.Id,
            "Enter the phone number for your Telegram account (e.g. +34612345678):");

        await _pending.SetPendingAction(new PendingActionHandler.PendingAction(
            "auth-phone",
            msg.Chat.Id,
            owner: this,
            callback: phone => HandlePhoneAsync(msg, phone)
        ));
    }

    private async Task HandlePhoneAsync(Message originalMsg, string phone)
    {
        try
        {
            await _bot.SendMessage(originalMsg.Chat.Id, "Connecting to Telegram...");
            await _userClient.BeginLoginAsync(_apiId, _apiHash, phone.Trim());
            await _bot.SendMessage(originalMsg.Chat.Id,
                "Telegram sent a verification code. Enter it:");

            await _pending.SetPendingAction(new PendingActionHandler.PendingAction(
                "auth-code",
                originalMsg.Chat.Id,
                owner: this,
                callback: code => HandleCodeAsync(originalMsg, code)
            ));
        }
        catch (Exception ex)
        {
            await _bot.SendMessage(originalMsg.Chat.Id, $"Auth error: {ex.Message}");
        }
    }

    private async Task HandleCodeAsync(Message originalMsg, string code)
    {
        try
        {
            var needsPassword = await _userClient.ProvideCodeAsync(code.Trim());

            if (!needsPassword)
            {
                await SendAuthComplete(originalMsg);
                return;
            }

            await _bot.SendMessage(originalMsg.Chat.Id,
                "Two-factor authentication is enabled. Enter your cloud password:");

            await _pending.SetPendingAction(new PendingActionHandler.PendingAction(
                "auth-password",
                originalMsg.Chat.Id,
                owner: this,
                callback: password => HandlePasswordAsync(originalMsg, password)
            ));
        }
        catch (Exception ex)
        {
            await _bot.SendMessage(originalMsg.Chat.Id, $"Auth error: {ex.Message}");
        }
    }

    private async Task HandlePasswordAsync(Message originalMsg, string password)
    {
        try
        {
            await _userClient.ProvidePasswordAsync(password.Trim());
            await SendAuthComplete(originalMsg);
        }
        catch (Exception ex)
        {
            await _bot.SendMessage(originalMsg.Chat.Id, $"Auth error: {ex.Message}");
        }
    }

    private async Task SendAuthComplete(Message originalMsg)
    {
        var premiumNote = _userClient.IsPremium
            ? $"Premium account detected. Upload limit: {_userClient.SplitLimitBytes / 1_000_000_000.0:F1} GB."
            : "Account is not Premium. Using standard 2 GB limit.";

        await _bot.SendMessage(originalMsg.Chat.Id,
            $"Authentication complete.\n{premiumNote}");
    }
}
