using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

[Callback(Id)]
public class AddCallback : ICallbackQuery
{
    public const string Id = "add";

    private readonly BotDispatcher _dispatcher;
    private readonly string _action;
    private readonly string _tmdbIdStr;
    private readonly string _mediaType;

    private AddCallback(BotDispatcher dispatcher, string action, string tmdbIdStr, string mediaType)
    {
        _dispatcher = dispatcher;
        _action = action;
        _tmdbIdStr = tmdbIdStr;
        _mediaType = mediaType;
    }

    public async Task ExecuteAsync(Message? message)
    {
        if (message == null) return;

        var bot = _dispatcher.Bot;
        var apiClient = _dispatcher.ApiClient;

        if (_action == "create" && int.TryParse(_tmdbIdStr, out var tmdbId))
        {
            try
            {
                await bot.DeleteMessages(message.Chat.Id, new[] { message.MessageId });
            }
            catch { /* Ignore if already deleted */ }

            var tempMsg = await bot.SendMessage(message.Chat.Id, "⏳ Adding item to the local library...");
            
            try
            {
                var ok = await apiClient.CreateManualMediaAsync(tmdbId, _mediaType);
                
                try
                {
                    await bot.DeleteMessages(tempMsg.Chat.Id, new[] { tempMsg.MessageId });
                }
                catch { /* Ignore */ }

                if (ok)
                {
                    var typeName = _mediaType == "movie" ? "Movie" : "Series";
                    await bot.SendMessage(message.Chat.Id, $"✅ {typeName} (TMDB ID: {tmdbId}) added successfully to the local library!");
                }
                else
                {
                    await bot.SendMessage(message.Chat.Id, $"❌ Error adding the item to the local library (it may already exist or there may be issues with TMDB).");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create manual media from callback", ex);
                try
                {
                    await bot.DeleteMessages(tempMsg.Chat.Id, new[] { tempMsg.MessageId });
                }
                catch { /* Ignore */ }
                await bot.SendMessage(message.Chat.Id, "❌ Connection error while creating the item.");
            }
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        var action = fields.Length > 0 ? fields[0] : "create";
        var tmdbIdStr = fields.Length > 1 ? fields[1] : "";
        var mediaType = fields.Length > 2 ? fields[2] : "movie";
        return new AddCallback(dispatcher, action, tmdbIdStr, mediaType);
    }

    public static string Pack(string action, string tmdbIdStr, string mediaType)
    {
        return CallbackDataPacker.Pack(Id, [action, tmdbIdStr, mediaType]);
    }
}
