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

    public static string FormatMovieHeader(Movie movie)
    {
        return $"🎬 {movie.Title} ({movie.ReleaseYear}) [tmdbid-{movie.TmdbId}]";
    }

    public static string FormatMovie(Movie movie)
    {
        return $"""
                
                <b>Title:</b> {movie.Title}
                <b>Release Year:</b> {movie.ReleaseYear}
                <b>ID:</b> {movie.Id}
                <b>TMDB ID:</b> <a href="https://www.themoviedb.org/movie/{movie.TmdbId}">{movie.TmdbId}</a>
                <b>Collections:</b> {movie.Collections!.Length}
                """;
    }
    public static string FormatSeries(Series series)
    {
        return $"""
                
                <b>Title:</b> {series.ManualTitle}
                <b>Release Year:</b> {series.ReleaseYear?.ToString() ?? "-"}
                <b>ID:</b> {series.Id}
                <b>TMDB ID:</b> <a href="https://www.themoviedb.org/tv/{series.TmdbId}">{series.TmdbId}</a>
                <b>Seasons:</b> {series.Seasons?.Length ?? 0}
                """;
    }


    public static string FormatCollection(Collection collection)
    {
        return $"""
                {Icons.CollectionIcon}Collection

                Name: {collection.Name}
                Collection Id: {collection.Id}
                Movie Id: {collection.MovieId?.ToString() ?? "-"}
                Quality: {collection.Quality ?? "-"}
                Audio Language: {collection.AudioLanguages ?? "-"}
                Subtitle Language: {collection.SubtitleLanguages ?? "-"}
                Tags: {collection.Tags ?? "-"}
                Notes: {collection.Notes ?? "-"}
                
                Files [{collection.Files!.Length}]:
                {string.Join("\n", collection.Files!.Select((f, i) => $"- {i + 1}. {f.FileName} ({FormatSize(f.FileSize)})"))}
                """;
    }
}