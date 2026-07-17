using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class PreviewCollectionCallback : ICallbackQuery
{
    private const string Id = "PreviewCollection";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _collectionId;

    public PreviewCollectionCallback(int collectionId, WTelegram.Bot bot, ApiClient apiClient)
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
            var error = $"Failed to get the collection with ID {_collectionId}";
            Log.Error(error);
            await _bot.SendMessage(message.Chat.Id, error);
            return;
        }

        if (collection.Files == null || collection.Files.Length == 0)
        {
            var error = $"Collection with ID {_collectionId} is empty";
            Log.Error(error);
            await _bot.SendMessage(message.Chat.Id, error);
            return;
        }

        if (collection.MovieId != null)
        {
            var movie = await _apiClient.GetMovieAsync(collection.MovieId.Value);

            if (movie != null)
            {
                var movieTitle = $"🎬 {movie.Title} ({movie.ReleaseYear})";
                if (movie.TmdbId.HasValue)
                    movieTitle += $"  •  <a href=\"https://www.themoviedb.org/movie/{movie.TmdbId}\">TMDB</a>";

                var text = Beautify.FormatCollectionPreviewHeader(collection, movieTitle);

                if (movie.PosterPath != null)
                {
                    await _bot.SendPhoto(message.Chat.Id,
                        MessageBuilder.FormatTmdbImageUrl(movie.PosterPath),
                        text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                }
                else
                {
                    await _bot.SendMessage(message.Chat.Id, text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                }
            }
        }

        var files = await _bot.GetMessagesById(message.Chat.Id, collection.Files.Select(f => f.MessageId));

        await SendCollectionFilesAsGroup(message.Chat.Id, files);
    }
    
    public Task SendCollectionFilesAsGroup(long chatId, List<WTelegram.Types.Message> files)
        => SendFilesAsGroup(_bot, chatId, files);

    public static async Task SendFilesAsGroup(WTelegram.Bot bot, long chatId, List<WTelegram.Types.Message> files)
    {
        const int maxPerGroup = 10;

        var validFiles = files.Where(f => f.Document != null || f.Video != null).ToList();
        if (validFiles.Count == 0)
        {
            await bot.SendMessage(chatId, "No valid files to send.");
            return;
        }

        for (int i = 0; i < validFiles.Count; i += maxPerGroup)
        {
            var group = validFiles.Skip(i).Take(maxPerGroup).ToList();

            var mediaGroup = new List<IAlbumInputMedia>();
            foreach (var file in group)
            {
                if (file.Document != null)
                    mediaGroup.Add(new InputMediaDocument(new InputFileId(file.Document.FileId)));
                else if (file.Video != null)
                    mediaGroup.Add(new InputMediaVideo(new InputFileId(file.Video.FileId)));
            }

            await bot.SendMediaGroup(chatId, mediaGroup);
        }
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new PreviewCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }

    public static string Pack(int collectionId)
    {
        return CallbackDataPacker.Pack(Id, new[] { collectionId.ToString() });
    }
}