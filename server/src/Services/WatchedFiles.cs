using Cinegram.Data;
using Cinegram.Models;
using Cinegram.Utils;
using Microsoft.EntityFrameworkCore;

namespace Cinegram.Services;

/// <summary>
/// The files found in the downloads folder (<c>watchedfile</c> rows) and their TMDB guess. A row
/// goes pending → notified once its Telegram message is out, confirmed/corrected when its identity
/// is settled, and moved/error after the move; removed when it disappears before being moved.
/// </summary>
public class WatchedFiles(IDbContextFactory<CinegramDb> dbFactory, ITmdbLookup tmdb)
{
    public static readonly string[] ResolvableStatuses = ["pending", "notified"];

    public sealed record Guess(
        string MediaType, int? TmdbId, string? Title, int? Year, int? Season, int? Episode,
        double Confidence, string Source);

    public enum ResolveError { None, NotFound, TmdbNotFound, TmdbFailed }

    public sealed record ResolveOutcome(WatchedFileResolution? Resolution, ResolveError Error, string? Detail = null);

    /// <summary>
    /// <see cref="Guess.Source"/> is "filename" when an explicit [tmdbid-N] tag in the filename
    /// resolved the identity (confidence 1.0), otherwise "tmdb" with the fuzzy match's similarity
    /// as confidence (0.0 when nothing matched or TMDB failed).
    /// </summary>
    public async Task<Guess> GuessAsync(string filename, CancellationToken ct = default)
    {
        var parsed = ReleaseNameParser.Parse(filename);

        TmdbTitle? match;
        try
        {
            match = await tmdb.IdentifyByFilenameAsync(filename, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log.Error($"[Watch] Failed to guess watched file '{filename}'", ex);
            match = null;
        }

        var fromTag = parsed.TmdbId is not null && match?.Id == parsed.TmdbId;

        return new Guess(
            match?.MediaType ?? parsed.Type,
            match?.Id,
            match?.DisplayTitle,
            match?.Year,
            parsed.Season,
            parsed.Episode,
            fromTag ? 1.0 : match?.MatchScore ?? 0.0,
            fromTag ? "filename" : "tmdb");
    }

    /// <summary>Idempotent per path. <c>Created</c> tells a new row from one already tracked.</summary>
    public async Task<(WatchedFile Row, bool Created)> ReportAsync(
        string path, string filename, long filesize, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.WatchedFiles.AsNoTracking().FirstOrDefaultAsync(w => w.Path == path, ct);
        if (existing is not null) return (existing, false);

        var row = new WatchedFile { Path = path, Filename = filename, Filesize = filesize };
        Apply(row, await GuessAsync(filename, ct));
        db.WatchedFiles.Add(row);
        await db.SaveChangesAsync(ct);
        return (row, true);
    }

    /// <summary>Follows a rename on disk, re-guessing rows that are still unresolved. Null when
    /// the old path isn't tracked.</summary>
    public async Task<WatchedFile?> RenameAsync(string oldPath, string newPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.WatchedFiles.FirstOrDefaultAsync(w => w.Path == oldPath, ct);
        if (row is null) return null;

        row.Path = newPath;
        row.Filename = Path.GetFileName(newPath);
        if (ResolvableStatuses.Contains(row.Status)) Apply(row, await GuessAsync(row.Filename, ct));

        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>
    /// Marks the row removed without deleting it. A no-op for rows already removed or moved (the
    /// latter disappear from the downloads folder because of the move itself). Null when the path
    /// isn't tracked.
    /// </summary>
    public async Task<WatchedFile?> MarkMissingAsync(string path, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.WatchedFiles.FirstOrDefaultAsync(w => w.Path == path, ct);
        if (row is null || row.Status is "removed" or "moved") return row;

        row.Status = "removed";
        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>Re-runs the guess for every unresolved row (after a parser fix, or if TMDB was down
    /// when a file was found). Settled rows are left alone.</summary>
    public async Task<List<WatchedFile>> ReidentifyAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.WatchedFiles.Where(w => ResolvableStatuses.Contains(w.Status)).ToListAsync(ct);

        foreach (var row in rows) Apply(row, await GuessAsync(row.Filename, ct));

        await db.SaveChangesAsync(ct);
        return rows;
    }

    /// <summary>Newest first, optionally filtered by status.</summary>
    public async Task<List<WatchedFile>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.WatchedFiles.AsNoTracking();
        if (!string.IsNullOrEmpty(status)) query = query.Where(w => w.Status == status);

        return await query.OrderByDescending(w => w.FirstSeenAt).ToListAsync(ct);
    }

    public async Task<WatchedFile?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.WatchedFiles.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    /// <summary>
    /// Settles the row's identity on <paramref name="tmdbId"/> (fetching the TVDB id for series,
    /// which the destination folder is tagged with) and sets <paramref name="status"/>, confirmed
    /// or corrected. Season and episode only change when given. Performs no file I/O.
    /// </summary>
    public async Task<ResolveOutcome> ResolveAsync(
        int id, int tmdbId, int? season, int? episode, string status, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.WatchedFiles.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (row is null) return new ResolveOutcome(null, ResolveError.NotFound, "Watched file not found");

        TmdbTitle? title;
        try
        {
            title = await tmdb.IdentifyByTmdbIdAsync(tmdbId, row.GuessMediaType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new ResolveOutcome(null, ResolveError.TmdbFailed, $"TMDB lookup failed: {ex.Message}");
        }

        if (title?.DisplayTitle is null) return new ResolveOutcome(null, ResolveError.TmdbNotFound, "TMDB id not found");

        row.GuessTmdbId = tmdbId;
        row.GuessMediaType = title.MediaType;
        row.GuessTitle = title.DisplayTitle;
        row.GuessYear = title.Year;
        row.GuessTvdbId = title.MediaType == "tv" ? await tmdb.GetTvdbIdAsync(tmdbId, ct) : null;
        if (season is not null) row.GuessSeason = season;
        if (episode is not null) row.GuessEpisode = episode;
        row.Confidence = 1.0;
        row.GuessSource = "filename";
        row.Status = status;

        await db.SaveChangesAsync(ct);
        return new ResolveOutcome(WatchedFileResolution.FromWatchedFile(row), ResolveError.None);
    }

    public Task MarkNotifiedAsync(int id, CancellationToken ct = default) =>
        UpdateAsync(id, row =>
        {
            row.Status = "notified";
            row.NotifiedAt = DateTime.UtcNow;
        }, ct);

    public Task MarkMovedAsync(int id, string movedPath, CancellationToken ct = default) =>
        UpdateAsync(id, row =>
        {
            row.Status = "moved";
            row.MovedPath = movedPath;
        }, ct);

    public Task MarkErrorAsync(int id, string errorMessage, CancellationToken ct = default) =>
        UpdateAsync(id, row =>
        {
            row.Status = "error";
            row.ErrorMessage = errorMessage;
        }, ct);

    private async Task UpdateAsync(int id, Action<WatchedFile> change, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.WatchedFiles.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (row is null) return;

        change(row);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(WatchedFile row, Guess guess)
    {
        row.GuessMediaType = guess.MediaType;
        row.GuessTmdbId = guess.TmdbId;
        row.GuessTitle = guess.Title;
        row.GuessYear = guess.Year;
        row.GuessSeason = guess.Season;
        row.GuessEpisode = guess.Episode;
        row.Confidence = guess.Confidence;
        row.GuessSource = guess.Source;
    }
}
