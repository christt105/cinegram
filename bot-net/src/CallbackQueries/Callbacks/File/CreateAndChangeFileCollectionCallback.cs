using Bot.CallbackQueries.Callbacks.Movie;
using Bot.Models;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class CreateAndChangeFileCollectionCallback : ICallbackQuery
{
    public const string Id = "createAndChangeFileCollection";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;

    private CreateAndChangeFileCollectionCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var file = await _apiClient.GetFileAsync(_fileId);

        if (file is null)
        {
            Log.Error($"File with ID {_fileId} not found");
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, $"File with ID {_fileId} not found");
            return;
        }

        int? movieId = null;
        int? previousCollectionId = null;

        if (file.CollectionId.HasValue)
        {
            var previousCollection = await _apiClient.GetCollectionAsync(file.CollectionId.Value);
            movieId = previousCollection?.MovieId;
            previousCollectionId = previousCollection?.Id;
        }

        var collection = await _apiClient.CreateCollectionAsync(new CreateCollectionRequest
        {
            Name = file.FileName,
            MovieId = movieId
        });

        if (collection == null)
        {
            Log.Error("Failed to create Collection");
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "Failed to create collection",
                replyMarkup: new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Back to File", ShowFileCallback.Pack(_fileId))
                    }
                });
            return;
        }

        var resultMessage =
            await _bot.SendMessage(message!.Chat.Id,
                $"New Collection created with ID {collection.Id} and Name {collection.Name}. Moving the file to the new Collection...");

        file = await _apiClient.PatchFileAsync(_fileId, new FileUpdate { CollectionId = collection.Id });

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

        if (previousCollectionId.HasValue)
            await new SeeMovieCollectionsCallback(previousCollectionId.Value, _bot, _apiClient).ExecuteAsync(message);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CreateAndChangeFileCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}