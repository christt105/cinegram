using Bot.Models;

namespace Bot.Utils;

public static class Beautify
{
    public static string FormatSize(long? bytes)
    {
        return bytes switch
        {
            > 1024L * 1024 * 1024 => $"{bytes / 1024f / 1024f / 1024f:0.00} GB",
            > 1024L * 1024 => $"{bytes / 1024f / 1024f:0.00} MB",
            > 1024 => $"{bytes / 1024f:0.00} KB",
            _ => $"{bytes} B"
        };
    }

    public static string FormatMovie(Movie movie)
    {
        return $"""
                Title: {movie.Title}
                Release Year: {movie.ReleaseYear}
                ID: {movie.Id}
                TMDB ID: [{movie.TmdbId}](https://www.themoviedb.org/movie/{movie.TmdbId})
                Collections: {movie.Collections!.Length}
                """;
    }
}