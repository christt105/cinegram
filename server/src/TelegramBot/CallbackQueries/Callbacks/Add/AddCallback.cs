using Telegram.Bot.Types;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Library;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Add;

[Callback(Id)]
public class AddCallback : ICallbackQuery
{
    public const string Id = "add";

    private readonly WTelegram.Bot _bot;
    private readonly CatalogIdentifier _identifier;
    private readonly string _action;
    private readonly string _tmdbIdStr;
    private readonly string _mediaType;

    private AddCallback(WTelegram.Bot bot, CatalogIdentifier identifier, string action, string tmdbIdStr, string mediaType)
    {
        _bot = bot;
        _identifier = identifier;
        _action = action;
        _tmdbIdStr = tmdbIdStr;
        _mediaType = mediaType;
    }

    public async Task ExecuteAsync(Message? message)
    {
        if (message == null) return;

        var bot = _bot;

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
                var ok = (await _identifier.CreateManualAsync(tmdbId, _mediaType)).Error
                         == CatalogIdentifier.CreateError.None;
                
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
                    await bot.SendMessage(message.Chat.Id, $"❌ Error adding the item to the local library (TMDB doesn't know that ID).");
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

    public static ICallbackQuery Create(string[] fields, WTelegram.Bot bot, CatalogIdentifier identifier)
    {
        var action = fields.Length > 0 ? fields[0] : "create";
        var tmdbIdStr = fields.Length > 1 ? fields[1] : "";
        var mediaType = fields.Length > 2 ? fields[2] : "movie";
        return new AddCallback(bot, identifier, action, tmdbIdStr, mediaType);
    }

    public static string Pack(string action, string tmdbIdStr, string mediaType)
    {
        return CallbackDataPacker.Pack(Id, [action, tmdbIdStr, mediaType]);
    }
}
