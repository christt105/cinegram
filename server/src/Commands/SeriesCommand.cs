using Cinegram.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = WTelegram.Types.Message;

namespace Cinegram.Commands;

public class SeriesCommand : ICommand
{
    private readonly Catalog _catalog;
    private readonly WTelegram.Bot _bot;

    public SeriesCommand(WTelegram.Bot bot, Catalog catalog)
    {
        _bot = bot;
        _catalog = catalog;
    }

    public async Task Execute(string[] args, Message msg)
    {
        var series = await _catalog.ListSeriesAsync();

        if (series == null || series.Count == 0)
        {
            await _bot.SendMessage(msg.Chat.Id, "📭 No series found.",
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return;
        }

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var s in series.Take(20))
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"📺 {s.ManualTitle} ({s.ReleaseYear})", Cinegram.CallbackQueries.Callbacks.Series.ShowSeriesCallback.Pack(s.Id))
            });
        }

        var replyMarkup = new InlineKeyboardMarkup(buttons);
        var text = $"📺 Series (showing top {Math.Min(series.Count, 20)}):";
        
        await _bot.SendMessage(msg.Chat.Id, text,
            replyMarkup: replyMarkup,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public string Key => "/series";
    public string Description => "Lists all series";
    public string Usage => "/series";
}
