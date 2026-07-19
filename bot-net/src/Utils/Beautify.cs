using System.Text.Json;
using Bot.Models;

namespace Bot.Utils;

public static class Beautify
{
    /// <summary>
    /// Turns the raw ffprobe JSON stored in a collection into a short one-line summary
    /// (size, bitrate, video codec, audio and subtitle languages). Returns an empty string
    /// when the metadata is missing or cannot be parsed.
    /// </summary>
    public static string FormatTechnicalSummary(string? technicalMetadata)
    {
        if (string.IsNullOrWhiteSpace(technicalMetadata))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(technicalMetadata);
            var root = doc.RootElement;
            var parts = new List<string>();

            if (root.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("size", out var size) && long.TryParse(size.GetString(), out var bytes))
                    parts.Add($"{bytes / 1024f / 1024f / 1024f:0.00} GB");
                if (format.TryGetProperty("bit_rate", out var bitRate) && long.TryParse(bitRate.GetString(), out var bps))
                    parts.Add($"{bps / 1_000_000f:0.0} Mbps");
            }

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                var video = streams.EnumerateArray()
                    .FirstOrDefault(s => StreamType(s) == "video");
                if (video.ValueKind == JsonValueKind.Object &&
                    video.TryGetProperty("codec_name", out var codec) && codec.GetString() is { } codecName)
                    parts.Add(codecName.ToUpperInvariant());

                var audios = LanguagesOfType(streams, "audio");
                if (audios.Count > 0)
                    parts.Add($"Aud: {string.Join(",", audios)}");

                var subs = LanguagesOfType(streams, "subtitle");
                if (subs.Count > 0)
                    parts.Add($"Sub: {string.Join(",", subs)}");
            }

            return string.Join("  |  ", parts);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? StreamType(JsonElement stream) =>
        stream.ValueKind == JsonValueKind.Object && stream.TryGetProperty("codec_type", out var t)
            ? t.GetString()
            : null;

    private static List<string> LanguagesOfType(JsonElement streams, string codecType)
    {
        var languages = new List<string>();
        foreach (var stream in streams.EnumerateArray())
        {
            if (StreamType(stream) != codecType) continue;
            if (!stream.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object) continue;

            var lang = (tags.TryGetProperty("language", out var l) ? l.GetString() : null)
                ?? (tags.TryGetProperty("LANGUAGE", out var u) ? u.GetString() : null);
            if (!string.IsNullOrWhiteSpace(lang) && !languages.Contains(lang))
                languages.Add(lang);
        }
        return languages;
    }

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

        var technical = FormatTechnicalSummary(collection.TechnicalMetadata);
        if (!string.IsNullOrWhiteSpace(technical))
            sb.AppendLine($"<b>Technical:</b> {technical}");

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

        var technical = FormatTechnicalSummary(collection.TechnicalMetadata);
        if (!string.IsNullOrWhiteSpace(technical))
            sb.AppendLine($"<b>Technical:</b> {technical}");

        sb.AppendLine();
        sb.AppendLine($"<b>Files: {collection.Files?.Length ?? 0}</b>");
        if (collection.Files != null)
            foreach (var (f, i) in collection.Files.Select((f, i) => (f, i)))
                sb.AppendLine($"{i + 1}. {f.FileName}  <i>({FormatSize(f.FileSize)})</i>");

        return sb.ToString().TrimEnd();
    }
}