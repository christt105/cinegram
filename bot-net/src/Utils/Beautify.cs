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
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>🎬 {movie.Title}</b> ({movie.ReleaseYear})");
        sb.AppendLine();
        sb.AppendLine($"<b>ID:</b> {movie.Id}");

        if (movie.TmdbId.HasValue)
            sb.AppendLine($"<b>TMDB:</b> <a href=\"https://www.themoviedb.org/movie/{movie.TmdbId}\">{movie.TmdbId}</a>");

        var collectionCount = movie.Collections?.Length ?? 0;
        sb.AppendLine($"<b>Collections:</b> {collectionCount}");

        if (!string.IsNullOrWhiteSpace(movie.Tags))
            sb.AppendLine($"<b>Tags:</b> {movie.Tags}");

        if (!string.IsNullOrWhiteSpace(movie.Notes))
            sb.AppendLine($"<b>Notes:</b> {movie.Notes}");

        if (!string.IsNullOrWhiteSpace(movie.Overview))
        {
            sb.AppendLine();
            sb.AppendLine($"<i>{movie.Overview}</i>");
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatSeries(Series series)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>📺 {series.ManualTitle}</b> ({series.ReleaseYear?.ToString() ?? "-"})");
        sb.AppendLine();
        sb.AppendLine($"<b>ID:</b> {series.Id}");

        if (series.TmdbId.HasValue)
            sb.AppendLine($"<b>TMDB:</b> <a href=\"https://www.themoviedb.org/tv/{series.TmdbId}\">{series.TmdbId}</a>");

        sb.AppendLine($"<b>Seasons:</b> {series.Seasons?.Length ?? 0}");

        if (!string.IsNullOrWhiteSpace(series.Overview))
        {
            sb.AppendLine();
            sb.AppendLine($"<i>{series.Overview}</i>");
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatCollectionQuality(Collection collection)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(collection.Quality)) parts.Add($"🎞 {collection.Quality}" );
        if (!string.IsNullOrWhiteSpace(collection.AudioLanguages)) parts.Add($"🔊 {collection.AudioLanguages}");
        if (!string.IsNullOrWhiteSpace(collection.SubtitleLanguages)) parts.Add($"💬 {collection.SubtitleLanguages}");
        return parts.Count > 0 ? string.Join("  |  ", parts) : string.Empty;
    }

    public static string FormatCollection(Collection collection)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{Icons.CollectionIcon} <b>{collection.Name}</b>");
        sb.AppendLine();
        sb.AppendLine($"<b>Collection ID:</b> {collection.Id}");

        if (!string.IsNullOrWhiteSpace(collection.Quality))
            sb.AppendLine($"<b>Quality:</b> {collection.Quality}");
        if (!string.IsNullOrWhiteSpace(collection.AudioLanguages))
            sb.AppendLine($"<b>Audio:</b> {collection.AudioLanguages}");
        if (!string.IsNullOrWhiteSpace(collection.SubtitleLanguages))
            sb.AppendLine($"<b>Subtitles:</b> {collection.SubtitleLanguages}");
        if (!string.IsNullOrWhiteSpace(collection.Tags))
            sb.AppendLine($"<b>Tags:</b> {collection.Tags}");
        if (!string.IsNullOrWhiteSpace(collection.Notes))
            sb.AppendLine($"<b>Notes:</b> {collection.Notes}");
        if (!string.IsNullOrWhiteSpace(collection.TechnicalMetadata))
            sb.AppendLine($"<b>Technical:</b> <code>{collection.TechnicalMetadata}</code>");

        sb.AppendLine();
        sb.AppendLine($"<b>Files [{collection.Files!.Length}]:</b>");
        foreach (var (f, i) in collection.Files!.Select((f, i) => (f, i)))
            sb.AppendLine($"{i + 1}. {f.FileName}  <i>({FormatSize(f.FileSize)})</i>");

        return sb.ToString().TrimEnd();
    }

    public static string FormatCollectionPreviewHeader(Collection collection, string mediaTitle)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{mediaTitle}</b>");
        sb.AppendLine($"<b>Collection:</b> {collection.Name}");

        var quality = FormatCollectionQuality(collection);
        if (!string.IsNullOrWhiteSpace(quality))
            sb.AppendLine(quality);

        if (!string.IsNullOrWhiteSpace(collection.Tags))
            sb.AppendLine($"<b>Tags:</b> {collection.Tags}");
        if (!string.IsNullOrWhiteSpace(collection.Notes))
            sb.AppendLine($"<b>Notes:</b> {collection.Notes}");
        if (!string.IsNullOrWhiteSpace(collection.TechnicalMetadata))
            sb.AppendLine($"<b>Technical:</b> <code>{collection.TechnicalMetadata}</code>");

        sb.AppendLine();
        sb.AppendLine($"<b>Files: {collection.Files?.Length ?? 0}</b>");
        if (collection.Files != null)
            foreach (var (f, i) in collection.Files.Select((f, i) => (f, i)))
                sb.AppendLine($"{i + 1}. {f.FileName}  <i>({FormatSize(f.FileSize)})</i>");

        return sb.ToString().TrimEnd();
    }
}