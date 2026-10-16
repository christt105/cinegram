using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;
using Cinegram.Infrastructure;
using Cinegram.Library;
using Cinegram.MediaFiles;
using Cinegram.TelegramBot.Handlers;

namespace Cinegram.TelegramBot.Commands;

public class ImportCommand : ICommand
{
    private readonly WTelegram.Bot _bot;
    private readonly Catalog _catalog;
    private readonly LibraryPaths _library;

    public ImportCommand(WTelegram.Bot bot, Catalog catalog, LibraryPaths library)
    {
        _bot = bot;
        _catalog = catalog;
        _library = library;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var moviesDir = _library.MoviesDir;
        var showsDir = _library.ShowsDir;

        await _bot.SendMessage(msg.Chat.Id,
            $"🔍 Scanning for media files…\nMovies: {moviesDir}\nShows: {showsDir}");

        var handler = new ImportHandler(_bot, _catalog);
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
