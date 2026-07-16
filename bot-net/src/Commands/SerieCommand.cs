using Bot.Services;
using Bot.Utils;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class SerieCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public SerieCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        if (args.Length == 0)
        {
            await _bot.SendMessage(msg.Chat.Id,
                "Please provide a series ID. Usage: `/serie <id>`",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        if (!int.TryParse(args[0], out var id))
        {
            await _bot.SendMessage(msg.Chat.Id,
                "Invalid series ID. Must be a number.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var series = await _apiClient.GetSeriesAsync(id);
        if (series == null)
        {
            Log.Error($"Failed to get series for {id}.");
            await _bot.SendMessage(msg.Chat.Id, $"Series with ID {id} not found.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var coverMessage = series.PosterPath != null
            ? await _bot.SendPhoto(msg.Chat.Id,
                MessageBuilder.FormatTmdbImageUrl(series.PosterPath),
                $"🎬 {series.ManualTitle} ({series.ReleaseYear}) [tmdbid-{series.TmdbId}]")
            : await _bot.SendMessage(msg.Chat.Id, $"🎬 {series.ManualTitle} ({series.ReleaseYear}) [tmdbid-{series.TmdbId}]");

        var infoText = Beautify.FormatSeries(series);

        var infoMessage = await _bot.SendMessage(
            msg.Chat.Id,
            infoText,
            ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: MessageBuilder.GetSeriesButtons(series.Id));
    }

    public string Key => "/serie";
    public string Description => "Gets details of a specific series by ID";
    public string Usage => "/serie <series-id>";
}