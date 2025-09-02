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
                
                Title: {movie.Title}
                Release Year: {movie.ReleaseYear}
                ID: {movie.Id}
                TMDB ID: [{movie.TmdbId}](https://www.themoviedb.org/movie/{movie.TmdbId})
                Collections: {movie.Collections!.Length}
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