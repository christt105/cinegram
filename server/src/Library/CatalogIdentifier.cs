using Microsoft.EntityFrameworkCore;
using Cinegram.Data;
using Cinegram.Infrastructure;
using Cinegram.Tmdb;

namespace Cinegram.Library;

/// <summary>
/// Links collections to the movie or series episode they hold, creating the movie, series, season
/// and episode rows on the way. Identifying one collection also links the unidentified collections
/// whose name parses to the same title and year.
/// </summary>
public class CatalogIdentifier(IDbContextFactory<CinegramDb> dbFactory, ITmdbLookup tmdb)
{
    /// <summary>
    /// With no <see cref="TmdbId"/> the title is guessed from the collection name, trying the
    /// library first. An explicit <see cref="MediaType"/> is taken as given; otherwise the one the
    /// name suggests is only tried first. <see cref="Season"/> and <see cref="Episode"/> override
    /// the numbers read from the name.
    /// </summary>
    public sealed record Request(int? TmdbId = null, string? MediaType = null, int? Season = null, int? Episode = null);

    public enum ReidentifyError { None, NotFound, TmdbNotFound }

    public sealed record ReidentifyOutcome(ReidentifyError Error, string? Title = null);

    public enum CreateError { None, InvalidMediaType, TmdbNotFound }

    public sealed record CreateOutcome(CreateError Error, string? Type = null, int Id = 0);

    /// <summary>False when the collection doesn't exist or nothing on TMDB matches. A collection
    /// already linked to a movie or episode is left alone unless a TMDB id is given.</summary>
    public async Task<bool> IdentifyAsync(int collectionId, Request request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var collection = await db.Collections.FindAsync([collectionId], ct);
        if (collection is null) return false;
        if (request.TmdbId is null && (collection.MovieId is not null || collection.EpisodeId is not null)) return true;

        var linked = await IdentifyAsync(db, collection, request, ct);
        await Catalog.PruneOrphanedMediaAsync(db, ct);
        return linked;
    }

    /// <summary>Unlinks the collection, then identifies it again.</summary>
    public async Task<bool> ReidentifyAsync(int collectionId, Request request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var collection = await db.Collections.FindAsync([collectionId], ct);
        if (collection is null) return false;

        collection.MovieId = null;
        collection.SeasonId = null;
        collection.EpisodeId = null;
        await db.SaveChangesAsync(ct);

        var linked = await IdentifyAsync(db, collection, request, ct);
        await Catalog.PruneOrphanedMediaAsync(db, ct);
        return linked;
    }

    /// <summary>Points the movie at another TMDB title. When the library already has that title,
    /// the collections move there and this movie is deleted.</summary>
    public async Task<ReidentifyOutcome> ReidentifyMovieAsync(int movieId, int tmdbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var movie = await db.Movies.FindAsync([movieId], ct);
        if (movie is null) return new(ReidentifyError.NotFound);

        var title = await tmdb.GetAsync("movie", tmdbId, ct);
        if (title is null) return new(ReidentifyError.TmdbNotFound);

        var collections = await db.Collections.Where(c => c.MovieId == movieId).OrderBy(c => c.Id).ToListAsync(ct);
        var target = await db.Movies.OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.TmdbId == tmdbId && m.Id != movieId, ct);
        if (target is not null)
        {
            foreach (var collection in collections) collection.MovieId = target.Id;
            await db.SaveChangesAsync(ct);
            await db.Movies.Where(m => m.Id == movieId).ExecuteDeleteAsync(ct);
        }
        else
        {
            target = movie;
            movie.TmdbId = tmdbId;
            movie.Title = title.Title ?? title.OriginalTitle;
            movie.PosterPath = title.PosterPath;
            movie.Overview = title.Overview;
            movie.ReleaseYear = title.Year;
            await db.SaveChangesAsync(ct);
        }

        foreach (var collection in collections)
            await PropagateAsync(db, collection, target, null, ct);
        await Catalog.PruneOrphanedMediaAsync(db, ct);
        return new(ReidentifyError.None, target.Title);
    }

    /// <summary>Points the series at another TMDB title, keeping each collection's season and
    /// episode. When the library already has that title, the collections move there and this
    /// series is deleted.</summary>
    public async Task<ReidentifyOutcome> ReidentifySeriesAsync(int seriesId, int tmdbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var series = await db.Series
            .Include(s => s.Seasons).ThenInclude(se => se.Collections)
            .Include(s => s.Seasons).ThenInclude(se => se.Episodes).ThenInclude(e => e.Collections)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct);
        if (series is null) return new(ReidentifyError.NotFound);

        var title = await tmdb.GetAsync("tv", tmdbId, ct);
        if (title is null) return new(ReidentifyError.TmdbNotFound);

        var collections = series.Seasons
            .SelectMany(se => se.Collections.Select(c => (Collection: c, Season: se.SeasonNumber, Episode: (int?)null))
                .Concat(se.Episodes.SelectMany(e => e.Collections.Select(c => (Collection: c, Season: se.SeasonNumber, Episode: (int?)e.EpisodeNumber)))))
            .ToList();

        var target = await db.Series.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.TmdbId == tmdbId && s.Id != seriesId, ct);
        if (target is not null)
        {
            foreach (var (collection, season, episode) in collections)
                await LinkEpisodeAsync(db, collection, target.Id, season, episode, ct);
            await db.SaveChangesAsync(ct);

            var seasonIds = series.Seasons.Select(se => se.Id).ToList();
            await db.Episodes.Where(e => seasonIds.Contains(e.SeasonId)).ExecuteDeleteAsync(ct);
            await db.Seasons.Where(se => seasonIds.Contains(se.Id)).ExecuteDeleteAsync(ct);
            await db.Series.Where(s => s.Id == seriesId).ExecuteDeleteAsync(ct);
        }
        else
        {
            target = series;
            series.TmdbId = tmdbId;
            series.ManualTitle = title.Name ?? title.OriginalName;
            series.PosterPath = title.PosterPath;
            series.Overview = title.Overview;
            series.ReleaseYear = title.Year;
            if (await tmdb.GetTvdbIdAsync(tmdbId, ct) is { } tvdbId) series.TvdbId = tvdbId;
            await db.SaveChangesAsync(ct);
        }

        foreach (var (collection, _, _) in collections)
            await PropagateAsync(db, collection, target, null, ct);
        await Catalog.PruneOrphanedMediaAsync(db, ct);
        return new(ReidentifyError.None, target.ManualTitle);
    }

    /// <summary>Adds a movie or series by hand. It is kept even while it has no collections.</summary>
    public async Task<CreateOutcome> CreateManualAsync(int tmdbId, string mediaType, CancellationToken ct = default)
    {
        if (mediaType is not ("movie" or "tv")) return new(CreateError.InvalidMediaType);

        var title = await tmdb.GetAsync(mediaType, tmdbId, ct);
        if (title is null) return new(CreateError.TmdbNotFound);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (mediaType == "movie")
        {
            var movie = await GetOrCreateMovieAsync(db, title, ct);
            movie.ManuallyAdded = true;
            await db.SaveChangesAsync(ct);
            return new(CreateError.None, "movie", movie.Id);
        }

        var series = await GetOrCreateSeriesAsync(db, title, ct);
        series.ManuallyAdded = true;
        await db.SaveChangesAsync(ct);
        return new(CreateError.None, "series", series.Id);
    }

    private async Task<bool> IdentifyAsync(CinegramDb db, Collection collection, Request request, CancellationToken ct)
    {
        var parsed = ReleaseNameParser.Parse(collection.Name ?? "");
        object? target;
        try
        {
            target = await ResolveAsync(db, collection, parsed, request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log.Error($"[Catalog] Failed to identify collection {collection.Id}", ex);
            return false;
        }

        if (target is null)
        {
            Log.Warning($"[Catalog] No TMDB match for collection {collection.Id} ({collection.Name})");
            return false;
        }

        var season = request.Season is not null ? request.Season : parsed.Season;
        var episode = request.Season is not null ? request.Episode : parsed.Episode;
        await LinkAsync(db, collection, target, season, episode, ct);
        await db.SaveChangesAsync(ct);
        Log.Info($"[Catalog] Linked collection {collection.Id} ({collection.Name}) to {Describe(target)}");

        await PropagateAsync(db, collection, target, parsed, ct);
        return true;
    }

    /// <summary>A <see cref="Movie"/> or <see cref="Series"/>, or null when nothing matches.</summary>
    private async Task<object?> ResolveAsync(
        CinegramDb db, Collection collection, ParsedReleaseName parsed, Request request, CancellationToken ct)
    {
        TmdbTitle? title;
        if (request.TmdbId is { } tmdbId)
        {
            title = request.MediaType is "movie" or "tv"
                ? await tmdb.GetAsync(request.MediaType, tmdbId, ct)
                : await tmdb.IdentifyByTmdbIdAsync(tmdbId, parsed.Type, ct);
        }
        else
        {
            var type = request.MediaType ?? parsed.Type;
            if (type == "tv")
            {
                var series = await db.Series.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.ManualTitle == parsed.CleanName, ct);
                if (series is not null)
                {
                    if (series is { TvdbId: null, TmdbId: { } seriesTmdbId })
                        series.TvdbId = await tmdb.GetTvdbIdAsync(seriesTmdbId, ct);
                    return series;
                }
            }
            else
            {
                var movie = await db.Movies.OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.Title == parsed.CleanName, ct);
                if (movie is not null) return movie;
            }

            title = await tmdb.IdentifyByFilenameAsync(collection.Name ?? "", ct);
        }

        return title switch
        {
            null => null,
            { MediaType: "movie" } => await GetOrCreateMovieAsync(db, title, ct),
            _ => await GetOrCreateSeriesAsync(db, title, ct),
        };
    }

    /// <summary>
    /// Links the unidentified collections whose name parses to the same title (and year, when both
    /// have one) to <paramref name="target"/>, each with its own season and episode.
    /// <paramref name="parsed"/> is the identified collection's name, parsed when null.
    /// </summary>
    private static async Task PropagateAsync(
        CinegramDb db, Collection identified, object target, ParsedReleaseName? parsed, CancellationToken ct)
    {
        parsed ??= ReleaseNameParser.Parse(identified.Name ?? "");
        var unidentified = await db.Collections
            .Where(c => c.Id != identified.Id && c.MovieId == null && c.SeasonId == null && c.EpisodeId == null)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        foreach (var sibling in unidentified)
        {
            var own = ReleaseNameParser.Parse(sibling.Name ?? "");
            if (own.CleanName != parsed.CleanName) continue;
            if (own.Year is not null && parsed.Year is not null && own.Year != parsed.Year) continue;

            await LinkAsync(db, sibling, target, own.Season, own.Episode, ct);
            Log.Info($"[Catalog] Propagated '{parsed.CleanName}' to collection {sibling.Id} ({sibling.Name})");
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A series collection without a season number goes to season 1 as a season pack.</summary>
    private static async Task LinkAsync(
        CinegramDb db, Collection collection, object target, int? season, int? episode, CancellationToken ct)
    {
        if (target is Movie movie)
        {
            collection.MovieId = movie.Id;
            collection.SeasonId = null;
            collection.EpisodeId = null;
            return;
        }

        var series = (Series)target;
        collection.MovieId = null;
        await LinkEpisodeAsync(db, collection, series.Id, season ?? 1, season is null ? null : episode, ct);
    }

    private static async Task LinkEpisodeAsync(
        CinegramDb db, Collection collection, int seriesId, int seasonNumber, int? episodeNumber, CancellationToken ct)
    {
        var season = await Catalog.GetOrCreateSeasonAsync(db, seriesId, seasonNumber, ct);
        if (episodeNumber is { } number)
        {
            var episode = await Catalog.GetOrCreateEpisodeAsync(db, season.Id, number, ct, $"Episode {number}");
            collection.EpisodeId = episode.Id;
            collection.SeasonId = null;
        }
        else
        {
            collection.SeasonId = season.Id;
            collection.EpisodeId = null;
        }
    }

    private static async Task<Movie> GetOrCreateMovieAsync(CinegramDb db, TmdbTitle title, CancellationToken ct)
    {
        var movie = await db.Movies.OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.TmdbId == title.Id, ct);
        if (movie is null)
        {
            movie = new Movie { TmdbId = title.Id, Title = title.Title ?? title.OriginalTitle };
            db.Movies.Add(movie);
        }

        movie.PosterPath ??= title.PosterPath;
        movie.Overview ??= title.Overview;
        movie.ReleaseYear ??= title.Year;
        await db.SaveChangesAsync(ct);
        return movie;
    }

    /// <summary>Also looks up the TVDB id, which the library naming prefers, when it's missing.</summary>
    private async Task<Series> GetOrCreateSeriesAsync(CinegramDb db, TmdbTitle title, CancellationToken ct)
    {
        var series = await db.Series.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.TmdbId == title.Id, ct);
        if (series is null)
        {
            series = new Series { TmdbId = title.Id, ManualTitle = title.Name ?? title.OriginalName };
            db.Series.Add(series);
        }

        series.TvdbId ??= await tmdb.GetTvdbIdAsync(title.Id, ct);
        series.PosterPath ??= title.PosterPath;
        series.Overview ??= title.Overview;
        series.ReleaseYear ??= title.Year;
        await db.SaveChangesAsync(ct);
        return series;
    }

    private static string Describe(object target) => target switch
    {
        Movie m => $"movie {m.Id} ({m.Title})",
        Series s => $"series {s.Id} ({s.ManualTitle})",
        _ => target.ToString() ?? "",
    };
}
