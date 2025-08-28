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

    public FileHandler(BotDispatcher bot)
    {
        _bot = bot.Bot;
        _queue = bot.Queue;
        _apiClient = bot.ApiClient;
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
        
        var message = @$"📥 New file received:
Id: {file.MessageId}
Name: {file.FileName}
Date: {file.UploadDate}
File size: {Beautify.FormatSize(file.FileSize)}
File extension: {file.MimeType}

Processed:
File ID: {result.Id}
Collection ID: {result.CollectionId}";

        await _bot.EditMessageText(answer.Chat.Id, answer.MessageId, message);
    }
}