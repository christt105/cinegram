using Bot.CallbackQueries;
using Bot.CallbackQueries.Callbacks;
using CallbackQuery = Telegram.Bot.Types.CallbackQuery;

namespace Bot.Handlers;

public class CallbackQueryHandler
{
    private readonly Dictionary<string, Func<string[], ICallbackQuery>> _factories;

    public CallbackQueryHandler(BotDispatcher bot)
    {
        _factories = new Dictionary<string, Func<string[], ICallbackQuery>>
        {
            {
                SeeMovieCollectionsCallback.Id,
                fields => SeeMovieCollectionsCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                BackToMovieCallback.Id,
                fields => BackToMovieCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                SeeCollectionFilesCallback.Id,
                fields => SeeCollectionFilesCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                EditMovieCallback.Id,
                fields => EditMovieCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                EditCollectionCallback.Id,
                fields => EditCollectionCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                DownloadMovieCallback.Id,
                fields => DownloadMovieCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                CreateCollectionCallback.Id,
                fields => CreateCollectionCallback.Create(fields, bot.Bot, bot.ApiClient)
            },
            {
                ShowFileCallback.Id,
                fields => ShowFileCallback.Create(fields, bot.Bot, bot.ApiClient)
            }
        };
    }

    public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data is null)
        {
            Log.Info("CallbackQuery data is null");
            return;
        }

        var data = callbackQuery.Data.Split(":");
        if (data.Length == 0)
        {
            Log.Info($"Failed to parse callback query data: {callbackQuery.Data}");
            return;
        }

        var id = data[0];
        var args = data[1..];

        if (!_factories.TryGetValue(id, out var factory))
        {
            Log.Error($"Callback query with id {id} not found");
            return;
        }

        var callback = factory(args);
        await callback.ExecuteAsync(callbackQuery.Message, callbackQuery);
    }
}