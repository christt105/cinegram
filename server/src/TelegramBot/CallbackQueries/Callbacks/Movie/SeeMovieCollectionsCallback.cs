using Telegram.Bot.Types.ReplyMarkups;
using Message = Telegram.Bot.Types.Message;
using Cinegram.Data;
using Cinegram.Library;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Collection;

namespace Cinegram.TelegramBot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class SeeMovieCollectionsCallback : ICallbackQuery
{
    public const string Id = "see-movie-collections";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;

    public SeeMovieCollectionsCallback(int movieId, WTelegram.Bot bot, Catalog catalog)
    {
        _movieId = movieId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collections = (await _catalog.GetMovieAsync(_movieId))?.Collections;

        var text = "Collections:\n\n";

        List<List<InlineKeyboardButton>> buttons = new();

        if (collections != null && collections.Count != 0)
            for (var i = 0; i < collections.Count; i++)
            {
                var c = collections[i];
                var qualityStr = string.IsNullOrWhiteSpace(c.Quality) ? "" : $" - Quality {c.Quality}";
                text += $"{i + 1}. \"{c.Name}\" (ID {c.Id}){qualityStr} - Files {c.Files.Count}\n";
                buttons.Add([
                    InlineKeyboardButton.WithCallbackData((i + 1).ToString(), ShowCollectionCallback.Pack(c.Id))
                ]);
            }

        buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("Create new Collection", CreateCollectionCallback.Pack(_movieId)),
                InlineKeyboardButton.WithCallbackData("\u2b05\ufe0f Back to movie", ShowMovieCallback.Pack(_movieId))
            ]
        );

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, replyMarkup: buttons.ToArray());
    }

    public static ICallbackQuery Create(string[] packedFields, BotDispatcher dispatcher)
    {
        return new SeeMovieCollectionsCallback(int.Parse(packedFields[0]), dispatcher.Bot, dispatcher.Catalog);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}