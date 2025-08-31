using Bot.CallbackQueries.Callbacks.File;
using Bot.CallbackQueries.Callbacks.Movie;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = Telegram.Bot.Types.Message;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class SeeCollectionFilesCallback : ICallbackQuery
{
    public const string Id = "see-collection-files";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _collectionId;

    private SeeCollectionFilesCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _collectionId = collectionId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _apiClient.GetCollectionAsync(_collectionId);

        if (collection is null)
        {
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId,
                $"Collection with Id {_collectionId} was not found");
            return;
        }

        var text = $"""
                    Collection

                    Name: {collection.Name}
                    Collection Id: {collection.Id}
                    Movie Id: {collection.MovieId}
                    Quality: {collection.Quality}
                    Audio Language: {collection.AudioLanguages}
                    Subtitle Language: {collection.SubtitleLanguages}
                    Tags: {collection.Tags}
                    Notes: {collection.Notes}
                    Files:
                    {string.Join("\n", collection.Files!.Select((f, i) => $"- {i + 1}. {f.FileName} ({Beautify.FormatSize(f.FileSize)})"))}
                    """;

        List<List<InlineKeyboardButton>> buttons = [];

        buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("Edit Collection", EditCollectionCallback.Pack(_collectionId)),
                InlineKeyboardButton.WithCallbackData("Delete Collection", DeleteCollectionCallback.Pack(_collectionId))
            ]
        );

        buttons.AddRange(collection.Files.Select((f, i) => new List<InlineKeyboardButton>
            { InlineKeyboardButton.WithCallbackData((i + 1).ToString(), ShowFileCallback.Pack(f.Id)) }));

        buttons.Add([
            InlineKeyboardButton.WithCallbackData("\u2b05\ufe0f Back to collection",
                SeeMovieCollectionsCallback.Pack(collection.MovieId!.Value))
        ]);

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, replyMarkup: buttons.ToArray());
    }

    public static ICallbackQuery Create(string[] packedFields, BotDispatcher dispatcher)
    {
        return new SeeCollectionFilesCallback(int.Parse(packedFields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }
}