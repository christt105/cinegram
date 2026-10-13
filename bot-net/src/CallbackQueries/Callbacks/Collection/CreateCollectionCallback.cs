using Bot.CallbackQueries.Callbacks.Movie;
using Bot.Handlers;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class CreateCollectionCallback : ICallbackQuery
{
    public const string Id = "create-collection";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _movieId;
    private readonly PendingActionHandler _pendingActionHandler;

    private CreateCollectionCallback(int movieId, WTelegram.Bot bot, Catalog catalog,
        PendingActionHandler pendingActionHandler)
    {
        _movieId = movieId;
        _bot = bot;
        _catalog = catalog;
        _pendingActionHandler = pendingActionHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.EditMessageText(message.Chat.Id, message.MessageId, "Type a Collection name:", replyMarkup: new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Cancel", ShowMovieCallback.Pack(_movieId))
            }
        });

        await _pendingActionHandler.SetPendingAction(
            new PendingActionHandler.PendingAction(
                Id,
                message.Chat.Id,
                message.MessageId,
                input => Callback(input, message),
                () => CancelCallback(message)));
    }

    private async Task Callback(string input, Message message)
    {
        await _catalog.CreateCollectionAsync(new Catalog.CollectionDraft(input.Trim(), MovieId: _movieId));

        await new SeeMovieCollectionsCallback(_movieId, _bot, _catalog).ExecuteAsync(message);
    }

    private async Task CancelCallback(Message? message)
    {
        await new SeeMovieCollectionsCallback(_movieId, _bot, _catalog).ExecuteAsync(message);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new CreateCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog,
            dispatcher.PendingActionHandler);
    }

    public static string Pack(int movieId)
    {
        return CallbackDataPacker.Pack(Id, [movieId.ToString()]);
    }
}