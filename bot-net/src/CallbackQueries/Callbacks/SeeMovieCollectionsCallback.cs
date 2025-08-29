using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = Telegram.Bot.Types.Message;

namespace Bot.CallbackQueries.Callbacks;

public class SeeMovieCollectionsCallback : ICallbackQuery
{
    public const string Id = "see-movie-collections";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;

    private SeeMovieCollectionsCallback(int movieId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _movieId = movieId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message, CallbackQuery callbackQuery)
    {
        var collections = await _apiClient.GetCollectionsAsync(_movieId);

        var text = "Collections:\n\n";

        List<List<InlineKeyboardButton>> buttons = new();

        if (collections != null && collections.Count != 0)
            for (var i = 0; i < collections.Count; i++)
            {
                var c = collections[i];
                text += $"{i + 1}. \"{c.Name}\" (ID {c.Id}) - Quality {c.Quality} - Files {c.Files!.Length}\n";
                buttons.Add([InlineKeyboardButton.WithCallbackData((i + 1).ToString(), SeeCollectionFilesCallback.Pack(c.Id))]);
            }

        buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("Create new Collection"),
                InlineKeyboardButton.WithCallbackData("\u2b05\ufe0f Back to movie", BackToMovieCallback.Pack(_movieId))
            ]
        );

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, replyMarkup: buttons.ToArray());
    }

    public static ICallbackQuery Create(string[] packedFields, WTelegram.Bot bot, ApiClient apiClient)
    {
        return new SeeMovieCollectionsCallback(int.Parse(packedFields[0]), bot, apiClient);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}