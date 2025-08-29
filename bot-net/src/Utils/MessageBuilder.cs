using Bot.CallbackQueries;
using Bot.CallbackQueries.Callbacks;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Utils;

public static class MessageBuilder
{
    public static InlineKeyboardButton[][] GetMovieButtons(int movieId)
    {
        return new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📂 Collections", SeeMovieCollectionsCallback.Pack(movieId)) },
            new[] { InlineKeyboardButton.WithCallbackData("⬇️ Download", DownloadMovieCallback.Pack(movieId)) },
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Edit Movie", EditMovieCallback.Pack(movieId)) }
        };
    }
}