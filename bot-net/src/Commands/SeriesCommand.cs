using Bot.Services;
using Telegram.Bot.Types;
using Message = WTelegram.Types.Message;

namespace Bot.Commands;

public class SeriesCommand : ICommand
{
    private readonly ApiClient _apiClient;
    private readonly WTelegram.Bot _bot;

    public SeriesCommand(WTelegram.Bot bot, ApiClient apiClient)
    {
        _bot = bot;
        _apiClient = apiClient;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var series = await _apiClient.GetSeriesAsync();

        var message = "📭 No series found.";
        if (series != null && series.Count > 0)
        {
            message = "📺 Series:\n";
            for (var i = 0; i < Math.Min(series.Count, 10); i++)
            {
                var s = series[i];
                message += $"- {s.Id}: {s.ManualTitle} [tmdbid-{s.TmdbId}]\n";
            }

            if (series.Count > 10) message += $"\n... and {series.Count - 10} more";
        }

        await _bot.SendMessage(msg.Chat.Id, message,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/series";
    public string Description => "Lists all series";
    public string Usage => "/series";
}
