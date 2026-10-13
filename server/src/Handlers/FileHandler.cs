using System.Text.Json;
using Cinegram;
using Cinegram.Services;
using Cinegram.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace Cinegram.Handlers;

public class FileHandler
{
    private readonly WTelegram.Bot _bot;
    private readonly Catalog _catalog;
    private readonly CatalogIdentifier _identifier;
    private readonly TaskQueue _queue;
    private readonly Cinegram.Handlers.PendingActionHandler _pendingActionHandler;

    public FileHandler(BotDispatcher bot)
    {
        _bot = bot.Bot;
        _queue = bot.Queue;
        _catalog = bot.Catalog;
        _identifier = bot.Identifier;
        _pendingActionHandler = bot.PendingActionHandler;
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        string? fileName = null;
        long fileSize = 0;
        string? mimeType = null;

        if (msg.Document != null)
        {
            fileName = msg.Document.FileName;
            fileSize = msg.Document.FileSize ?? 0;
            mimeType = msg.Document.MimeType;
        }
        else if (msg.Video != null)
        {
            fileName = msg.Video.FileName;
            fileSize = msg.Video.FileSize ?? 0;
            mimeType = msg.Video.MimeType;
        }

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = $"video_{msg.MessageId}.mp4";
        }

        var forward = ForwardMetadata.Extract(msg.ForwardOrigin, msg.TLMessage);

        var uploadFile = new Catalog.FileUpload(
            msg.MessageId,
            fileName,
            fileSize,
            mimeType ?? "video/mp4",
            msg.Date,
            DocumentId: forward.DocumentId,
            FwdFromType: forward.FromType,
            FwdFromId: forward.FromId,
            FwdFromName: forward.FromName,
            FwdFromHidden: forward.Hidden);

        var message = @$"📥 New file received:
Id: {uploadFile.MessageId}
Name: {uploadFile.Filename}
Date: {uploadFile.CreatedAt:O}
File size: {Beautify.FormatSize(uploadFile.Filesize)}
File extension: {uploadFile.MimeType}

Starting to process...";

        Log.Info($"File received {message}");

        var answer = await _bot.SendMessage(
            msg.Chat.Id,
            message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });

        await _queue.Enqueue(() => IdentifyFile(uploadFile, answer));
    }

    private async Task IdentifyFile(Catalog.FileUpload file, Message answer)
    {
        var result = await _catalog.AddFileAsync(file);

        Log.Info(JsonSerializer.Serialize(result));

        if (result.Duplicate)
        {
            var message = @$"⚠️ Already in the library, this is a re-forward of a file you already have:
Id: {file.MessageId}
Name: {file.Filename}
Collection ID: {result.CollectionId}";
            await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
            return;
        }

        if (result.MovieId == null && result.EpisodeId == null && result.SeasonId == null)
        {
            var message = @$"⚠️ Could not automatically identify the file.
Id: {file.MessageId}
Name: {file.Filename}

Please reply to this message with the numeric TMDB ID to link it manually.";

            await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
            
            await _pendingActionHandler.SetPendingAction(new Cinegram.Handlers.PendingActionHandler.PendingAction(
                id: $"identify_{result.CollectionId}",
                chatId: answer.Chat.Id,
                owner: this,
                callback: async (text) =>
                {
                    if (int.TryParse(text.Trim(), out var tmdbId))
                    {
                        await _bot.SendMessage(answer.Chat.Id, $"Attempting to link with TMDB ID {tmdbId}...");
                        var success = await _identifier.IdentifyAsync(result.CollectionId, new CatalogIdentifier.Request(tmdbId));
                        if (success)
                        {
                            await _bot.SendMessage(answer.Chat.Id, "✅ File linked successfully.");
                        }
                        else
                        {
                            await _bot.SendMessage(answer.Chat.Id, "❌ An error occurred while linking the file (possibly an incorrect ID).");
                        }
                    }
                    else
                    {
                        await _bot.SendMessage(answer.Chat.Id, "❌ Invalid ID. Operation cancelled.");
                    }
                },
                cancelCallback: async () =>
                {
                    await _bot.SendMessage(answer.Chat.Id, "Link operation cancelled.");
                }
            ));
        }
        else
        {
            var message = @$"✅ File processed and identified successfully:
Id: {file.MessageId}
Name: {file.Filename}
Movie ID: {result.MovieId}
Season ID: {result.SeasonId}
Episode ID: {result.EpisodeId}
Collection ID: {result.CollectionId}";
            await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
        }
    }
}