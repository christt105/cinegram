using Telegram.Bot.Types.ReplyMarkups;
using Cinegram.Data;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Collection;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Movie;
using Cinegram.TelegramBot.CallbackQueries.Callbacks.Series;

namespace Cinegram.TelegramBot;

public static class MessageBuilder
{
    public static InlineKeyboardButton[][] GetMovieButtons(int movieId)
    {
        return new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📂 Collections", SeeMovieCollectionsCallback.Pack(movieId)) },
            new[] { InlineKeyboardButton.WithCallbackData("⬇️ Download", DownloadMovieCallback.Pack(movieId)) },
            new[] { InlineKeyboardButton.WithCallbackData("🎬 Info & Files", SelectCollectionToPreview.Pack(movieId)) },
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Edit Movie", EditMovieCallback.Pack(movieId)) }
        };
    }
    
    public static InlineKeyboardButton[][] GetSeriesButtons(int seriesId)
    {
        return new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📂 Seasons & Episodes", SeeSeriesSeasonsCallback.Pack(seriesId)) },
            new[] { InlineKeyboardButton.WithCallbackData("⬇️ Download", DownloadSeriesCallback.Pack(seriesId)) },
            new[] { InlineKeyboardButton.WithCallbackData("🎬 Info & Files", SelectSeasonToPreview.Pack(seriesId)) },
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Edit Series", EditSeriesCallback.Pack(seriesId)) }
        };
    }
    
    public static string FormatTmdbImageUrl(string tmdbImageUrl)
    {
        return $"https://image.tmdb.org/t/p/w500{tmdbImageUrl}";
    }
}