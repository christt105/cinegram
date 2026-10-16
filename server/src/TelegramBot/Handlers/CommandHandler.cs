using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;
using Cinegram.Library;
using Cinegram.TelegramBot.Commands;

namespace Cinegram.TelegramBot.Handlers;

public class CommandHandler
{
    private readonly WTelegram.Bot _bot;

    private readonly Dictionary<string, ICommand> _commands;

    /// <summary><paramref name="registered"/> in menu order; /help is added last, listing them all.</summary>
    public CommandHandler(WTelegram.Bot bot, IEnumerable<ICommand> registered)
    {
        _bot = bot;

        var commands = registered.ToList();
        commands.Add(new HelpCommand(bot, commands.ToArray()));

        _commands = commands.ToDictionary(c => c.Key, c => c);

        if (_commands.Any(c => !c.Key.StartsWith('/')))
        {
            var invalidCommands = string.Join(", ",
                _commands.Select(c => !c.Key.StartsWith('/'))
            );
            Log.Error(
                $"Commands {invalidCommands} will be ignored as it needs to be prefixed with `/`");
        }
    }

    // Commands shown in the Telegram "/" menu (command without the leading slash, as Telegram expects).
    public IEnumerable<BotCommand> GetMenuCommands()
    {
        return _commands.Values
            .Where(c => c.Key.StartsWith('/'))
            .Select(c => new BotCommand { Command = c.Key.TrimStart('/'), Description = c.Description });
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        var parts = msg.Text.Split(" ");
        var command = parts[0];
        var args = parts.Skip(1).ToArray();

        if (!_commands.TryGetValue(command, out var commandHandler))
        {
            await _bot.SendMessage(msg.Chat.Id,
                $"Command {command} is not recognized. Use /help to get the available commands.");
            return;
        }

        await commandHandler.Execute(args, msg);
    }
}