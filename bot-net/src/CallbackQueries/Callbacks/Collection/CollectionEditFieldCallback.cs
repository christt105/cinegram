using Bot.Handlers;
using Bot.Models;
using Bot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.CallbackQueries.Callbacks.Collection;

[Callback(Id)]
public class CollectionEditFieldCallback : ICallbackQuery
{
    public const string Id = "collectionEditFieldCallback";

    public const string NameField = "name";
    public const string QualityField = "quality";
    public const string AudioField = "audio";
    public const string SubtitlesField = "subtitles";
    public const string TagsField = "tags";
    public const string NotesField = "notes";

    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    private readonly int _collectionId;
    private readonly string _field;
    private readonly PendingActionHandler _pendingActionHandler;

    private readonly string? _setValue;

    public CollectionEditFieldCallback(int collectionId, string field, WTelegram.Bot bot, ApiClient apiClient,
        PendingActionHandler pendingActionHandler, string? setValue = null)
    {
        _collectionId = collectionId;
        _field = field;
        _bot = bot;
        _apiClient = apiClient;
        _pendingActionHandler = pendingActionHandler;
        _setValue = setValue;
    }

    public async Task ExecuteAsync(Message? message)
    {
        if (_setValue != null)
        {
            _pendingActionHandler.Clear();
            await Callback(_setValue, message!);
            return;
        }

        var collection = await _apiClient.GetCollectionAsync(_collectionId);

        if (collection is null)
        {
            var errorMessage = $"Failed to get collection with ID {_collectionId}";
            Log.Error(errorMessage);
            await _bot.SendMessage(message.Chat.Id, errorMessage);
            return;
        }


        var buttons = GetButtons(_field)
            .Concat(new[]
                { new[] { InlineKeyboardButton.WithCallbackData("Cancel", CancelPendingActionCallback.Pack()) } })
            .ToArray();


        var text = $"""
                    ✏️ Editing *{_field}*

                    Current value: `{GetValue(collection, _field)}`

                    Press the value to copy.

                    {GetInstructions(_field)}

                    Type a new value:
                    """;

        await _bot.EditMessageText(message!.Chat.Id, message.MessageId, text, replyMarkup: buttons,
            parseMode: ParseMode.MarkdownV2);

        await _pendingActionHandler.SetPendingAction(new PendingActionHandler.PendingAction(
            Id,
            message.Chat.Id,
            message.MessageId,
            input => Callback(input, message),
            async () =>
            {
                await new EditCollectionCallback(_collectionId, _bot, _apiClient).ExecuteAsync(message);
                _pendingActionHandler.Clear();
            }
        ));
    }

    private InlineKeyboardButton[][] GetButtons(string field)
    {
        return field switch
        {
            //TODO: Set class with Qualities
            QualityField =>
            [
                [
                    InlineKeyboardButton.WithCallbackData("480p", Pack(_collectionId, QualityField, "480p")),
                    InlineKeyboardButton.WithCallbackData("720p", Pack(_collectionId, QualityField, "720p"))
                ],
                [
                    InlineKeyboardButton.WithCallbackData("1080p", Pack(_collectionId, QualityField, "1080p")),
                    InlineKeyboardButton.WithCallbackData("4K", Pack(_collectionId, QualityField, "4K"))
                ],
                [InlineKeyboardButton.WithCallbackData("4K HDR", Pack(_collectionId, QualityField, "4K HDR"))]
            ],
            _ => []
        };
    }


    private string GetInstructions(string field)
    {
        return field switch
        {
            QualityField => "Choose from the common options below or type your own:",
            AudioField => """
                          Enter the audio languages using [ISO](https://www.localeplanet.com/icu/index.html) codes, separated by commas.

                          ISO examples:
                          - en (English)
                          - es (Spanish)
                          - es-es (Spanish - Spain)
                          - es-419 (Spanish - Latin America, general)
                          - ca (Catalan)
                          - gl (Galician)
                          - eu (Basque / Euskera)
                          - ja (Japanese)

                          Example:
                          - `en,es`
                          - `es,en,ja`
                          """,
            SubtitlesField => """
                              Enter the subtitle languages using [ISO](https://www.localeplanet.com/icu/index.html) codes, separated by commas.

                              ISO examples:
                              - `en` (English)
                              - `es` (Spanish)
                              - `es-es` (Spanish - Spain)
                              - `es-419` (Spanish - Latin America, general)
                              - `ca` (Catalan)
                              - `gl` (Galician)
                              - `eu` (Basque / Euskera)
                              - `ja` (Japanese)

                              Multiple languages:
                              - `en,es`
                              - `es,en,ja`
                              """,
            TagsField => """
                         Add one or more tags separated by commas. 
                         Examples:
                         - action,adventure
                         - anime
                         - family,kids
                         """,
            NotesField => """
                          Add any free text notes about this collection.
                          Example:
                          - extended version with director commentary
                          """,
            _ => string.Empty
        };
    }


    private static string GetValue(Models.Collection collection, string field)
    {
        switch (field)
        {
            case NameField:
                return collection.Name;
            case QualityField:
                return collection.Quality ?? "-";
            case AudioField:
                return collection.AudioLanguages ?? "-";
            case SubtitlesField:
                return collection.SubtitleLanguages ?? "-";
            case TagsField:
                return collection.Tags ?? "-";
            case NotesField:
                return collection.Notes ?? "-";
            default:
                Log.Error("Unknown field type");
                return "Unknown";
        }
    }

    private async Task Callback(string input, Message message)
    {
        _pendingActionHandler.Clear();
        
        var update = new UpdateCollectionRequest();

        switch (_field)
        {
            case NameField:
                update.Name = input.Trim();
                break;
            case QualityField:
                update.Quality = input.Trim();
                break;
            case AudioField:
                update.AudioLanguages = input.Trim();
                break;
            case SubtitlesField:
                update.SubtitleLanguages = input.Trim();
                break;
            case TagsField:
                update.Tags = input.Trim();
                break;
            case NotesField:
                update.Notes = input.Trim();
                break;
        }

        var collection =
            await _apiClient.PatchCollectionAsync(_collectionId, update);

        if (collection is null)
        {
            var errorMessage = $"Failed to patch collection with ID {_collectionId}";
            Log.Error(errorMessage);
            await _bot.SendMessage(message.Chat.Id, errorMessage);
            return;
        }

        await new EditCollectionCallback(_collectionId, _bot, _apiClient).ExecuteAsync(message);
    }


    public static string Pack(int collectionId, string field, string? setValue = null)
    {
        return CallbackDataPacker.Pack(Id,
            setValue != null ? [collectionId.ToString(), field, setValue] : [collectionId.ToString(), field]);
    }

    public static ICallbackQuery Create(string[] fields, BotDispatcher dispatcher)
    {
        if (fields.Length == 2)
            return new CollectionEditFieldCallback(int.Parse(fields[0]), fields[1], dispatcher.Bot,
                dispatcher.ApiClient,
                dispatcher.PendingActionHandler);
        return new CollectionEditFieldCallback(int.Parse(fields[0]), fields[1], dispatcher.Bot, dispatcher.ApiClient,
            dispatcher.PendingActionHandler, fields[2]);
    }
}