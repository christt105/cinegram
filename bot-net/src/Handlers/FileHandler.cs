using System.Text.Json;
using Bot;
using Bot.Models;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace TelegramBot.Handlers;

public class FileHandler
{
    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;
    private readonly TaskQueue _queue;
    private readonly Bot.Handlers.PendingActionHandler _pendingActionHandler;

    public FileHandler(BotDispatcher bot)
    {
        _bot = bot.Bot;
        _queue = bot.Queue;
        _apiClient = bot.ApiClient;
        _pendingActionHandler = bot.PendingActionHandler;
    }

    public async Task Handle(Message msg, UpdateType type)
    {
        var uploadFile = new UploadFile
        {
            MessageId = msg.MessageId,
            FileName = msg.Document.FileName,
            FileSize = msg.Document.FileSize,
            UploadDate = msg.Date.ToString("O"),
            MimeType = msg.Document.MimeType
        };

        var message = @$"📥 New file received:
Id: {uploadFile.MessageId}
Name: {uploadFile.FileName}
Date: {uploadFile.UploadDate}
File size: {Beautify.FormatSize(uploadFile.FileSize)}
File extension: {uploadFile.MimeType}

Starting to process...";

        Log.Info($"File received {message}");

        var answer = await _bot.SendMessage(
            msg.Chat.Id,
            message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });

        await _queue.Enqueue(() => IdentifyFile(uploadFile, answer));
    }

    private async Task IdentifyFile(UploadFile file, Message answer)
    {
        var result = await _apiClient.UploadAsync(file);

        Log.Info(JsonSerializer.Serialize(result));
        
        if (result.MovieId == null && result.EpisodeId == null && result.SeasonId == null && result.CollectionId.HasValue)
        {
            var message = @$"⚠️ No pude identificar automáticamente el archivo.
Id: {file.MessageId}
Name: {file.FileName}

Por favor, responde a este mensaje con el ID de TMDB numérico para enlazarla manualmente.";

            await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
            
            await _pendingActionHandler.SetPendingAction(new Bot.Handlers.PendingActionHandler.PendingAction(
                id: $"identify_{result.CollectionId}",
                chatId: answer.Chat.Id,
                owner: this,
                callback: async (text) =>
                {
                    if (int.TryParse(text.Trim(), out var tmdbId))
                    {
                        await _bot.SendMessage(answer.Chat.Id, $"Intentando enlazar con TMDB ID {tmdbId}...");
                        var success = await _apiClient.IdentifyCollectionAsync(result.CollectionId.Value, tmdbId);
                        if (success)
                        {
                            await _bot.SendMessage(answer.Chat.Id, "✅ Archivo enlazado correctamente.");
                        }
                        else
                        {
                            await _bot.SendMessage(answer.Chat.Id, "❌ Hubo un error al enlazar el archivo (posible ID incorrecto).");
                        }
                    }
                    else
                    {
                        await _bot.SendMessage(answer.Chat.Id, "❌ ID inválido. Operación cancelada.");
                    }
                },
                cancelCallback: async () =>
                {
                    await _bot.SendMessage(answer.Chat.Id, "Operación de enlace cancelada.");
                }
            ));
        }
        else
        {
            var message = @$"✅ Archivo procesado e identificado correctamente:
Id: {file.MessageId}
Name: {file.FileName}
Movie ID: {result.MovieId}
Season ID: {result.SeasonId}
Episode ID: {result.EpisodeId}
Collection ID: {result.CollectionId}";
            await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
        }
    }
}