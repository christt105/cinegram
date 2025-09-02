using Bot.Services;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Movie;

[Callback(Id)]
public class DownloadMovieCallback : ICallbackQuery
{
    private readonly int _collectionId;
    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;

    public DownloadMovieCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _collectionId = collectionId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public const string Id = "download-movie";

    public async Task ExecuteAsync(Message? message)
    {
        var collection = await _apiClient.GetCollectionAsync(_collectionId);

        if (collection is null)
        {
            var error = $"Failed to get the collection with ID {_collectionId}";
            Log.Error(error);
            await _bot.SendMessage(message.Chat.Id, error);
            return;
        }
        
        //collection.
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, [collectionId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new DownloadMovieCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}