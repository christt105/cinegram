using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class EditCollectionCallback : ICallbackQuery
{
    public const string Id = "edit-collection";
    
    private readonly int _collectionId; 
    private readonly WTelegram.Bot _bot; 
    private readonly ApiClient _apiClient; 
    

    public EditCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _collectionId = collectionId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _apiClient.GetCollectionAsync(_collectionId);

        if (collection == null)
        {
            Log.Error($"Failed to get the collection with ID {_collectionId}.");
            await _bot.SendMessage(message!.Chat.Id, $"Failed to get the collection with ID {_collectionId}.");
            return;
        }

        var text = Beautify.FormatCollection(collection);

        text += """

                What do you want to edit?
                """;
        
        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text,
            replyMarkup: new[]
            {
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Name", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.NameField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Quality", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.QualityField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Audio", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.AudioField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Subtitles", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.SubtitlesField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Tags", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.TagsField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Edit Notes", CollectionEditFieldCallback.Pack(_collectionId, CollectionEditFieldCallback.NotesField))},
                new[]{ InlineKeyboardButton.WithCallbackData("Cancel", ShowCollectionCallback.Pack(_collectionId))}
            });
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new EditCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}