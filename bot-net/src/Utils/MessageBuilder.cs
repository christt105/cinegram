using Bot.CallbackQueries.Callbacks.Collection;
using Bot.CallbackQueries.Callbacks.Movie;
using Telegram.Bot.Types.ReplyMarkups;
using Bot.CallbackQueries.Callbacks.Series;

namespace Bot.Utils;

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