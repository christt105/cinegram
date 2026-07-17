using Bot.CallbackQueries.Callbacks.Collection;
using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.File;

[Callback(Id)]
public class ShowFileCallback : ICallbackQuery
{
    public const string Id = "show-file";
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;
    private readonly int _fileId;

    public ShowFileCallback(int fileId, WTelegram.Bot bot, ApiClient apiClient)
    {
        _fileId = fileId;
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task ExecuteAsync(Message? message)
    {
        var file = await _apiClient.GetFileAsync(_fileId);

        if (file is null)
        {
            await _bot.EditMessageText(message!.Chat.Id, message.MessageId, $"File {_fileId} not found");
            return;
        }

        var collection = await _apiClient.GetCollectionAsync(file.CollectionId.Value);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{Icons.FileIcon} <b>File #{_fileId}</b>");
        sb.AppendLine();
        sb.AppendLine($"<b>Filename:</b> <code>{file.FileName}</code>");
        sb.AppendLine($"<b>Size:</b> {Beautify.FormatSize(file.FileSize)}");
        sb.AppendLine($"<b>Type:</b> {file.MimeType ?? "-"}");
        sb.AppendLine($"<b>Added:</b> {file.CreatedAt}");

        if (collection != null)
        {
            sb.AppendLine();
            sb.AppendLine($"<b>Collection:</b> {collection.Name} (ID {file.CollectionId})");
            var quality = Beautify.FormatCollectionQuality(collection);
            if (!string.IsNullOrWhiteSpace(quality))
                sb.AppendLine(quality);
            if (!string.IsNullOrWhiteSpace(collection.TechnicalMetadata))
                sb.AppendLine($"\n<b>Technical:</b> <code>{collection.TechnicalMetadata}</code>");
        }

        var text = sb.ToString().TrimEnd();

        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑 Delete File", DeleteFileCallback.Parse(_fileId))
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📂 Change Collection", FileChangeCollectionCallback.Parse(_fileId))
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Back to collection",
                    ShowCollectionCallback.Pack(file.CollectionId.Value))
            }
        };

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text,
            Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: buttons);
    }

    public static string Pack(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        return new ShowFileCallback(int.Parse(fields[0]), dispatcher.Bot, dispatcher.ApiClient);
    }
}