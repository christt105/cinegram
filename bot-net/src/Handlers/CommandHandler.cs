using Bot.Commands;
using Telegram.Bot.Types.Enums;
using WTelegram.Types;

namespace Bot.Handlers;

public class CommandHandler
{
    private readonly BotDispatcher _bot;

    private readonly Dictionary<string, ICommand> _commands;

    public CommandHandler(BotDispatcher bot)
    {
        _bot = bot;

        var commands = new ICommand[]
        {
            new HealthCommand(bot.Bot, bot.ApiClient)
        };

        commands = commands.Append(new HelpCommand(bot.Bot, commands)).ToArray();

        _commands = commands.ToDictionary(c => c.Key, c => c);
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        var parts = msg.Text.Split(" ");
        var command = parts[0];
        var args = parts.Skip(1).ToArray();

        if (!_commands.TryGetValue(command, out var commandHandler))
        {
            await _bot.Bot.SendMessage(msg.Chat.Id,
                $"Command {command} is not recognized. Use /help to get the available commands.");
            return;
        }

        await commandHandler.Execute(args, msg);
    }
}