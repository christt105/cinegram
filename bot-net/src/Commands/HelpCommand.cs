using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class HelpCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly ICommand[] _commands;

    public HelpCommand(WTelegram.Bot bot, ICommand[] commands)
    {
        _bot = bot;
        _commands = new ICommand[] { this }.Concat(commands).ToArray();
    }

    public async Task Execute(string[] args, Message msg)
    {
        var text = "📖 Available commands:\n\n";

        foreach (var command in _commands)
            text += $"{command.Key} - {command.Description}\nUsage: {command.Usage}\n\n";

        await _bot.SendMessage(msg.Chat.Id, text, replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/help";
    public string Description => "Shows a list of available commands.";
    public string Usage => "/help";
}