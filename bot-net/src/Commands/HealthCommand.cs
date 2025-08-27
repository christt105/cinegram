using System.Text.Json;
using Bot.Services;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class HealthCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public HealthCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var result = await _apiClient.HealthAsync();

        await _bot.SendMessage(msg.Chat.Id, JsonSerializer.Serialize(result),
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/health";
    public string Description => "Get the health of the bot.";
    public string Usage => "/health";
}