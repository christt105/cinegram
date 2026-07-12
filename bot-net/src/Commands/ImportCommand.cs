using Bot.Handlers;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class ImportCommand : ICommand
{
    private readonly BotDispatcher _dispatcher;

    public ImportCommand(BotDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task Execute(string[] args, Message msg)
    {
        var moviesDir = Environment.GetEnvironmentVariable("IMPORT_MOVIES_DIR") ?? "/data/import/movies";
        var showsDir = Environment.GetEnvironmentVariable("IMPORT_SHOWS_DIR") ?? "/data/import/shows";

        await _dispatcher.Bot.SendMessage(msg.Chat.Id,
            $"🔍 Scanning for media files…\nMovies: {moviesDir}\nShows: {showsDir}");

        var handler = new ImportHandler(_dispatcher.Bot, _dispatcher.ApiClient);
        _ = Task.Run(async () =>
        {
            try { await handler.Run(msg.Chat.Id, moviesDir, showsDir); }
            catch (Exception ex) { Log.Error("Import task failed unexpectedly", ex); }
        });
    }

    public string Key => "/import";
    public string Description => "Imports local media files to Telegram and the database.";
    public string Usage => "/import";
}
