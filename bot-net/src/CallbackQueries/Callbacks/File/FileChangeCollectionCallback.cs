using Bot.CallbackQueries.Callbacks.Collection;
using Bot.Handlers;
using Bot.Models;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class FileChangeCollectionCallback : ICallbackQuery
{
    public const string Id = "file-change-collection";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;
    private readonly PendingActionHandler _pendingActionHandler;

    public FileChangeCollectionCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient,
        PendingActionHandler pendingActionHandler)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
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
                    InlineKeyboardButton.WithCallbackData("Cancel", CancelPendingActionCallback.Pack(_fileId))
                }
            });

        await _pendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            Id,
            message.Chat.Id,
            message.MessageId,
            input => Callback(input, message),
            async () =>
            {
                var callback = new ShowFileCallback(_fileId, _bot, _apiClient);
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
            await new ShowFileCallback(_fileId, _bot, _apiClient).ExecuteAsync(message);
            return;
        }

        var previousCollectionId = (await _apiClient.GetFileAsync(_fileId))?.CollectionId;
        var file = await _apiClient.PatchFileAsync(_fileId, new FileUpdate { CollectionId = collectionId });

        if (file is null)
        {
            Log.Error($"Failed to change collection ID for file {_fileId}.");
            await _bot.SendMessage(message.Chat.Id, "Failed to change collection ID. Operation cancelled.");
            await new ShowFileCallback(_fileId, _bot, _apiClient).ExecuteAsync(message);
            return;
        }

        Log.Info($"Collection ID for file {_fileId} changed to {collectionId}");
        await _bot.SendMessage(message.Chat.Id, $"Collection ID changed to {collectionId}.");
        if (previousCollectionId.HasValue)
            await new SeeCollectionFilesCallback(previousCollectionId.Value, _bot, _apiClient)
                .ExecuteAsync(message);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new FileChangeCollectionCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient,
            dispatcher.PendingActionHandler);
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}