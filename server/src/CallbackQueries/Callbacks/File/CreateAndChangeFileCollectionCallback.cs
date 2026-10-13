using Cinegram.CallbackQueries.Callbacks.Movie;
using Cinegram.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Cinegram.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class CreateAndChangeFileCollectionCallback : ICallbackQuery
{
    public const string Id = "createAndChangeFileCollection";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;

    private CreateAndChangeFileCollectionCallback(int fileId, WTelegram.Bot bot, Catalog catalog)
    {
        _fileId = fileId;
        _bot = bot;
        _catalog = catalog;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var file = await _catalog.GetFileAsync(_fileId);

        if (file is null)
        {
            Log.Error($"File with ID {_fileId} not found");
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, $"File with ID {_fileId} not found");
            return;
        }

        var movieId = (await _catalog.GetCollectionAsync(file.CollectionId))?.MovieId;
        var collection = await _catalog.CreateCollectionAsync(new Catalog.CollectionDraft(file.Filename, MovieId: movieId));

        var resultMessage =
            await _bot.SendMessage(message!.Chat.Id,
                $"New Collection created with ID {collection.Id} and Name {collection.Name}. Moving the file to the new Collection...");

        (file, _) = await _catalog.MoveFileAsync(_fileId, collection.Id);

        if (file is null)
        {
            Log.Error($"Could not patch file with ID {_fileId}");
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "Failed to patch the file",
                replyMarkup: new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Back to File", ShowFileCallback.Pack(_fileId))
                    }
                });
            await _bot.EditMessageText(resultMessage.Chat.Id, resultMessage.MessageId,
                $"New Collection created with ID {collection.Id} and Name {collection.Name}. Failed to move the file to the new Collection.");
            return;
        }

        await _bot.EditMessageText(resultMessage.Chat.Id, resultMessage.MessageId,
            $"New Collection created with ID {collection.Id} and Name {collection.Name}. File with ID {_fileId} moved to the new collection with ID {collection.Id}.");

        if (movieId.HasValue)
            await new SeeMovieCollectionsCallback(movieId.Value, _bot, _catalog).ExecuteAsync(message);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CreateAndChangeFileCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog);
    }
}