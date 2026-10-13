using Cinegram.CallbackQueries.Callbacks.Collection;
using Cinegram.Handlers;
using Cinegram.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Cinegram.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class FileChangeCollectionCallback : ICallbackQuery
{
    public const string Id = "file-change-collection";
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;
    private readonly PendingActionHandler _pendingActionHandler;

    public FileChangeCollectionCallback(int fileId, WTelegram.Bot bot, Catalog catalog,
        PendingActionHandler pendingActionHandler)
    {
        _fileId = fileId;
        _bot = bot;
        _catalog = catalog;
        _pendingActionHandler = pendingActionHandler;
    }

    public async Task ExecuteAsync(Message? message)
    {
        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, "Type a new collection ID for the file:",
            replyMarkup: new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Create a new Collection with this file", CreateAndChangeFileCollectionCallback.Pack(_fileId)),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", CancelPendingActionCallback.Pack())
                }
            });

        await _pendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            Id,
            message.Chat.Id,
            message.MessageId,
            input => Callback(input, message),
            async () =>
            {
                var callback = new ShowFileCallback(_fileId, _bot, _catalog);
                await callback.ExecuteAsync(message);
                _pendingActionHandler.Clear();
            }
        ));
    }

    private async Task Callback(string input, Message message)
    {
        if (!int.TryParse(input.Trim(), out var collectionId))
        {
            Log.Error("Invalid collection ID input");
            await _bot.SendMessage(message.Chat.Id, "Invalid collection ID. Operation cancelled.");
            await new ShowFileCallback(_fileId, _bot, _catalog).ExecuteAsync(message);
            return;
        }

        var previousCollectionId = (await _catalog.GetFileAsync(_fileId))?.CollectionId;
        var (file, _) = await _catalog.MoveFileAsync(_fileId, collectionId);

        if (file is null)
        {
            Log.Error($"Failed to change collection ID for file {_fileId}.");
            await _bot.SendMessage(message.Chat.Id, "Failed to change collection ID. Operation cancelled.");
            await new ShowFileCallback(_fileId, _bot, _catalog).ExecuteAsync(message);
            return;
        }

        Log.Info($"Collection ID for file {_fileId} changed to {collectionId}");
        await _bot.SendMessage(message.Chat.Id, $"Collection ID changed to {collectionId}.");
        if (previousCollectionId.HasValue)
            await new ShowCollectionCallback(previousCollectionId.Value, _bot, _catalog)
                .ExecuteAsync(message);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new FileChangeCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.Catalog,
            dispatcher.PendingActionHandler);
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}