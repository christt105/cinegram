using Bot.Data;
using Bot.Utils;
using Microsoft.EntityFrameworkCore;

namespace Bot.Services;

/// <summary>
/// The library: movies, series with their seasons and episodes, the collections linked to them and
/// the Telegram files in each collection. Reads return detached graphs with every collection's
/// files loaded. Changes that can leave a movie or series without collections prune it, unless it
/// was added by hand.
/// </summary>
public class Catalog(IDbContextFactory<CinegramDb> dbFactory, CatalogIdentifier identifier)
{
    public sealed record FileUpload(
        int MessageId,
        string Filename,
        long Filesize,
        string? MimeType,
        DateTime CreatedAt,
        int? UserMessageId = null,
        int? TmdbId = null,
        string? TechnicalMetadata = null,
        string StoragePeer = "bot",
        long? DocumentId = null,
        string? FwdFromType = null,
        string? FwdFromId = null,
        string? FwdFromName = null,
        bool FwdFromHidden = false);

    /// <summary><see cref="Duplicate"/> is true when the upload is a forward of a blob already
    /// stored under another message; nothing is added then.</summary>
    public sealed record UploadOutcome(
        int FileId, int MessageId, string Filename, int CollectionId, int? MovieId, int? SeasonId, int? EpisodeId,
        bool Duplicate);

    public sealed record CollectionDraft(
        string Name,
        int? MovieId = null,
        int? SeasonId = null,
        int? EpisodeId = null,
        string? Quality = null,
        string? Codec = null,
        string? AudioLanguages = null,
        string? SubtitleLanguages = null,
        string? Tags = null,
        string? Notes = null);

    /// <summary>Only the fields that were set are written. <see cref="SeasonNumber"/>,
    /// <see cref="EpisodeNumber"/> and <see cref="ClearEpisode"/> move a series collection to
    /// another season or episode instead.</summary>
    public sealed class CollectionChanges
    {
        private readonly Dictionary<string, string?> _fields = new();

        public IReadOnlyDictionary<string, string?> Fields => _fields;

        public string? Name { get => Get("name"); set => _fields["name"] = value; }
        public string? Quality { get => Get("quality"); set => _fields["quality"] = value; }
        public string? Codec { get => Get("codec"); set => _fields["codec"] = value; }
        public string? AudioLanguages { get => Get("audio_languages"); set => _fields["audio_languages"] = value; }
        public string? SubtitleLanguages { get => Get("subtitle_languages"); set => _fields["subtitle_languages"] = value; }
        public string? Tags { get => Get("tags"); set => _fields["tags"] = value; }
        public string? Notes { get => Get("notes"); set => _fields["notes"] = value; }
        public string? TechnicalMetadata { get => Get("technical_metadata"); set => _fields["technical_metadata"] = value; }
        public string? LocalPath { get => Get("local_path"); set => _fields["local_path"] = value; }

        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public bool ClearEpisode { get; set; }

        public bool Has(string field) => _fields.ContainsKey(field);

        private string? Get(string field) => _fields.GetValueOrDefault(field);
    }

    public async Task<List<Movie>> ListMoviesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await MoviesWithFiles(db).OrderBy(m => m.Id).ToListAsync(ct);
    }

    public async Task<Movie?> GetMovieAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await MoviesWithFiles(db).FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<Movie?> GetMovieByTmdbAsync(int tmdbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await MoviesWithFiles(db).OrderBy(m => m.Id).FirstOrDefaultAsync(m => m.TmdbId == tmdbId, ct);
    }

    public async Task<List<Series>> ListSeriesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await SeriesWithFiles(db).OrderBy(s => s.Id).ToListAsync(ct);
    }

    public async Task<Series?> GetSeriesAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await SeriesWithFiles(db).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Series?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await SeriesWithFiles(db).OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.TmdbId == tmdbId, ct);
    }

    public async Task<Collection?> GetCollectionAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Collections.AsNoTracking().Include(c => c.Files).FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<TelegramFile?> GetFileAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    /// <summary>Collections linked to no movie, season or episode.</summary>
    public async Task<List<Collection>> ListOrphansAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Collections.AsNoTracking().Include(c => c.Files)
            .Where(c => c.MovieId == null && c.SeasonId == null && c.EpisodeId == null)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Stores a file sent to Telegram. A message already stored only gets its missing user id and
    /// forward origin filled in. A video gets a collection of its own; other files (archive parts)
    /// join the collection named like them. A new or still unidentified collection is identified
    /// against TMDB, by <see cref="FileUpload.TmdbId"/> when given.
    /// </summary>
    public async Task<UploadOutcome> AddFileAsync(FileUpload upload, CancellationToken ct = default)
    {
        TelegramFile file;
        Collection collection;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var existing = await db.Files.Include(f => f.Collection)
                .OrderBy(f => f.Id).FirstOrDefaultAsync(f => f.MessageId == upload.MessageId, ct);
            if (existing is not null)
            {
                if (upload.UserMessageId is not null && existing.UserMessageId is null)
                    existing.UserMessageId = upload.UserMessageId;
                if (upload.DocumentId is not null && existing.DocumentId is null)
                {
                    existing.DocumentId = upload.DocumentId;
                    existing.FwdFromType = upload.FwdFromType;
                    existing.FwdFromId = upload.FwdFromId;
                    existing.FwdFromName = upload.FwdFromName;
                    existing.FwdFromHidden = upload.FwdFromHidden;
                }
                await db.SaveChangesAsync(ct);
                (file, collection) = (existing, existing.Collection!);
            }
            else
            {
                if (upload.DocumentId is not null)
                {
                    var duplicate = await db.Files.AsNoTracking().Include(f => f.Collection)
                        .OrderBy(f => f.Id).FirstOrDefaultAsync(f => f.DocumentId == upload.DocumentId, ct);
                    if (duplicate is not null) return Outcome(duplicate, duplicate.Collection!, true);
                }

                collection = await CollectionForAsync(db, upload, ct);
                file = new TelegramFile
                {
                    MessageId = upload.MessageId,
                    UserMessageId = upload.UserMessageId,
                    Filename = upload.Filename,
                    Filesize = upload.Filesize,
                    MimeType = upload.MimeType,
                    CreatedAt = upload.CreatedAt.ToUniversalTime(),
                    Collection = collection,
                    StoragePeer = upload.StoragePeer,
                    DocumentId = upload.DocumentId,
                    FwdFromType = upload.FwdFromType,
                    FwdFromId = upload.FwdFromId,
                    FwdFromName = upload.FwdFromName,
                    FwdFromHidden = upload.FwdFromHidden,
                };
                db.Files.Add(file);
                await db.SaveChangesAsync(ct);
            }
        }

        if (collection.MovieId is null && collection.SeasonId is null && collection.EpisodeId is null)
        {
            await identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request(upload.TmdbId), ct);
            collection = (await GetCollectionAsync(collection.Id, ct))!;
        }

        return Outcome(file, collection, false);
    }

    public async Task<Collection> CreateCollectionAsync(CollectionDraft draft, CancellationToken ct = default)
    {
        var collection = new Collection
        {
            Name = draft.Name,
            MovieId = draft.MovieId,
            SeasonId = draft.SeasonId,
            EpisodeId = draft.EpisodeId,
            Quality = draft.Quality,
            Codec = draft.Codec,
            AudioLanguages = CollectionMetadata.ValidateLanguages(draft.AudioLanguages),
            SubtitleLanguages = CollectionMetadata.ValidateLanguages(draft.SubtitleLanguages),
            Tags = draft.Tags,
            Notes = draft.Notes,
        };

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);
        return collection;
    }

    /// <summary>
    /// Null when the collection doesn't exist. Probed technical metadata fills in quality and codec
    /// unless the same change sets them.
    /// </summary>
    /// <exception cref="InvalidLanguageCodeException">A language is outside the vocabulary.</exception>
    public async Task<Collection?> UpdateCollectionAsync(int id, CollectionChanges changes, CancellationToken ct = default)
    {
        var audio = changes.Has("audio_languages") ? CollectionMetadata.ValidateLanguages(changes.AudioLanguages) : null;
        var subtitles = changes.Has("subtitle_languages") ? CollectionMetadata.ValidateLanguages(changes.SubtitleLanguages) : null;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var collection = await db.Collections.Include(c => c.Episode).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null) return null;

        if (changes.SeasonNumber is { } seasonNumber && await SeriesIdOfAsync(db, collection, ct) is { } seriesId)
            await MoveToSeasonAsync(db, collection, seriesId, seasonNumber, changes, ct);

        foreach (var (field, value) in changes.Fields)
        {
            switch (field)
            {
                case "name": collection.Name = value; break;
                case "quality": collection.Quality = value; break;
                case "codec": collection.Codec = value; break;
                case "audio_languages": collection.AudioLanguages = audio; break;
                case "subtitle_languages": collection.SubtitleLanguages = subtitles; break;
                case "tags": collection.Tags = value; break;
                case "notes": collection.Notes = value; break;
                case "technical_metadata": collection.TechnicalMetadata = value; break;
                case "local_path": collection.LocalPath = value; break;
            }
        }

        if (!string.IsNullOrEmpty(changes.TechnicalMetadata))
        {
            if (!changes.Has("quality") && CollectionMetadata.QualityFromProbe(changes.TechnicalMetadata) is { } quality)
                collection.Quality = quality;
            if (!changes.Has("codec") && CollectionMetadata.CodecFromProbe(changes.TechnicalMetadata) is { } codec)
                collection.Codec = codec;
        }

        await db.SaveChangesAsync(ct);
        await PruneOrphanedMediaAsync(db, ct);
        return collection;
    }

    /// <summary>Deletes the collection with its files and pending downloads.</summary>
    public async Task<bool> DeleteCollectionAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Collections.AnyAsync(c => c.Id == id, ct)) return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DeleteCollectionsAsync(db, [id], ct);
        await PruneOrphanedMediaAsync(db, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Unlinks its collections, which stay as orphans, and deletes the movie.</summary>
    public async Task<bool> DeleteMovieAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Movies.AnyAsync(m => m.Id == id, ct)) return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Collections.Where(c => c.MovieId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.MovieId, (int?)null), ct);
        await db.Movies.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
        await PruneOrphanedMediaAsync(db, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Unlinks its collections, which stay as orphans, and deletes the series with its
    /// seasons and episodes.</summary>
    public async Task<bool> DeleteSeriesAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Series.AnyAsync(s => s.Id == id, ct)) return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DeleteSeriesTreeAsync(db, [id], ct);
        await PruneOrphanedMediaAsync(db, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<Movie?> UpdateMovieAsync(int id, Action<Movie> apply, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var movie = await db.Movies.FindAsync([id], ct);
        if (movie is null) return null;
        apply(movie);
        await db.SaveChangesAsync(ct);
        return movie;
    }

    public async Task<Series?> UpdateSeriesAsync(int id, Action<Series> apply, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var series = await db.Series.FindAsync([id], ct);
        if (series is null) return null;
        apply(series);
        await db.SaveChangesAsync(ct);
        return series;
    }

    public async Task<Season?> UpdateSeasonAsync(int seriesId, int seasonNumber, Action<Season> apply, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var season = await FindSeasonAsync(db, seriesId, seasonNumber, ct);
        if (season is null) return null;
        apply(season);
        await db.SaveChangesAsync(ct);
        return season;
    }

    public enum EpisodeUpdateError { None, SeasonNotFound, EpisodeNotFound }

    public async Task<(Episode? Episode, EpisodeUpdateError Error)> UpdateEpisodeAsync(
        int seriesId, int seasonNumber, int episodeNumber, Action<Episode> apply, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var season = await FindSeasonAsync(db, seriesId, seasonNumber, ct);
        if (season is null) return (null, EpisodeUpdateError.SeasonNotFound);

        var episode = await db.Episodes.OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.SeasonId == season.Id && e.EpisodeNumber == episodeNumber, ct);
        if (episode is null) return (null, EpisodeUpdateError.EpisodeNotFound);

        apply(episode);
        await db.SaveChangesAsync(ct);
        return (episode, EpisodeUpdateError.None);
    }

    public enum FileMoveError { None, FileNotFound, CollectionNotFound }

    /// <summary>Moves one file to another collection, leaving the old one as it is even if empty.</summary>
    public async Task<(TelegramFile? File, FileMoveError Error)> MoveFileAsync(int fileId, int collectionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var file = await db.Files.FindAsync([fileId], ct);
        if (file is null) return (null, FileMoveError.FileNotFound);
        if (!await db.Collections.AnyAsync(c => c.Id == collectionId, ct)) return (null, FileMoveError.CollectionNotFound);

        file.CollectionId = collectionId;
        await db.SaveChangesAsync(ct);
        return (file, FileMoveError.None);
    }

    /// <summary>Moves the files that exist to <paramref name="collectionId"/> and deletes the
    /// collections they leave empty. False when the target collection doesn't exist.</summary>
    public async Task<bool> MoveFilesAsync(IReadOnlyCollection<int> fileIds, int collectionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Collections.AnyAsync(c => c.Id == collectionId, ct)) return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var files = await db.Files.Where(f => fileIds.Contains(f.Id)).ToListAsync(ct);
        var sources = files.Select(f => f.CollectionId).Where(id => id != collectionId).Distinct().ToList();
        foreach (var file in files) file.CollectionId = collectionId;
        await db.SaveChangesAsync(ct);

        await DeleteEmptyCollectionsAsync(db, sources, ct);
        await PruneOrphanedMediaAsync(db, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteFileAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Files.Where(f => f.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    /// <summary>Deletes the files that exist and the collections they leave empty.</summary>
    public async Task DeleteFilesAsync(IReadOnlyCollection<int> fileIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var sources = await db.Files.Where(f => fileIds.Contains(f.Id)).Select(f => f.CollectionId).Distinct().ToListAsync(ct);
        await db.Files.Where(f => fileIds.Contains(f.Id)).ExecuteDeleteAsync(ct);

        await DeleteEmptyCollectionsAsync(db, sources, ct);
        await PruneOrphanedMediaAsync(db, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Deletes movies and series left without any collection. Ones added by hand stay, so files can
    /// be attached to them later. Runs SQL directly, so call it after the context's own changes.
    /// </summary>
    internal static async Task PruneOrphanedMediaAsync(CinegramDb db, CancellationToken ct)
    {
        await db.Movies.Where(m => !m.ManuallyAdded && !m.Collections.Any()).ExecuteDeleteAsync(ct);

        var emptySeries = await db.Series
            .Where(s => !s.ManuallyAdded
                        && !s.Seasons.Any(se => se.Collections.Any() || se.Episodes.Any(e => e.Collections.Any())))
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (emptySeries.Count > 0) await DeleteSeriesTreeAsync(db, emptySeries, ct);
    }

    internal static async Task<Season> GetOrCreateSeasonAsync(CinegramDb db, int seriesId, int seasonNumber, CancellationToken ct)
    {
        var season = await FindSeasonAsync(db, seriesId, seasonNumber, ct);
        if (season is not null) return season;

        season = new Season { SeriesId = seriesId, SeasonNumber = seasonNumber };
        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);
        return season;
    }

    internal static async Task<Episode> GetOrCreateEpisodeAsync(
        CinegramDb db, int seasonId, int episodeNumber, CancellationToken ct, string? title = null)
    {
        var episode = await db.Episodes.OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.SeasonId == seasonId && e.EpisodeNumber == episodeNumber, ct);
        if (episode is not null) return episode;

        episode = new Episode { SeasonId = seasonId, EpisodeNumber = episodeNumber, Title = title };
        db.Episodes.Add(episode);
        await db.SaveChangesAsync(ct);
        return episode;
    }

    private static Task<Season?> FindSeasonAsync(CinegramDb db, int seriesId, int seasonNumber, CancellationToken ct) =>
        db.Seasons.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.SeriesId == seriesId && s.SeasonNumber == seasonNumber, ct);

    private static async Task DeleteSeriesTreeAsync(CinegramDb db, List<int> seriesIds, CancellationToken ct)
    {
        var seasonIds = await db.Seasons.Where(s => seriesIds.Contains(s.SeriesId)).Select(s => s.Id).ToListAsync(ct);
        var episodeIds = await db.Episodes.Where(e => seasonIds.Contains(e.SeasonId)).Select(e => e.Id).ToListAsync(ct);

        await db.Collections.Where(c => c.EpisodeId != null && episodeIds.Contains(c.EpisodeId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EpisodeId, (int?)null), ct);
        await db.Collections.Where(c => c.SeasonId != null && seasonIds.Contains(c.SeasonId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.SeasonId, (int?)null), ct);
        await db.Episodes.Where(e => episodeIds.Contains(e.Id)).ExecuteDeleteAsync(ct);
        await db.Seasons.Where(s => seasonIds.Contains(s.Id)).ExecuteDeleteAsync(ct);
        await db.Series.Where(s => seriesIds.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }

    private static async Task DeleteCollectionsAsync(CinegramDb db, List<int> collectionIds, CancellationToken ct)
    {
        await db.DownloadTasks.Where(t => collectionIds.Contains(t.CollectionId)).ExecuteDeleteAsync(ct);
        await db.Files.Where(f => collectionIds.Contains(f.CollectionId)).ExecuteDeleteAsync(ct);
        await db.Collections.Where(c => collectionIds.Contains(c.Id)).ExecuteDeleteAsync(ct);
    }

    private static async Task DeleteEmptyCollectionsAsync(CinegramDb db, List<int> collectionIds, CancellationToken ct)
    {
        var empty = await db.Collections.Where(c => collectionIds.Contains(c.Id) && !c.Files.Any())
            .Select(c => c.Id).ToListAsync(ct);
        if (empty.Count > 0) await DeleteCollectionsAsync(db, empty, ct);
    }

    private static async Task<int?> SeriesIdOfAsync(CinegramDb db, Collection collection, CancellationToken ct)
    {
        if (collection.SeasonId is { } seasonId)
            return await db.Seasons.Where(s => s.Id == seasonId).Select(s => (int?)s.SeriesId).FirstOrDefaultAsync(ct);
        if (collection.EpisodeId is { } episodeId)
            return await db.Episodes.Where(e => e.Id == episodeId).Select(e => (int?)e.Season!.SeriesId).FirstOrDefaultAsync(ct);
        return null;
    }

    /// <summary>An episode collection keeps its episode number in the new season unless
    /// <see cref="CollectionChanges.EpisodeNumber"/> or <see cref="CollectionChanges.ClearEpisode"/>
    /// say otherwise; a season collection stays a season pack.</summary>
    private static async Task MoveToSeasonAsync(
        CinegramDb db, Collection collection, int seriesId, int seasonNumber, CollectionChanges changes, CancellationToken ct)
    {
        var season = await GetOrCreateSeasonAsync(db, seriesId, seasonNumber, ct);
        var episodeNumber = changes.ClearEpisode ? null : changes.EpisodeNumber ?? collection.Episode?.EpisodeNumber;

        if (episodeNumber is { } number)
        {
            var episode = await GetOrCreateEpisodeAsync(db, season.Id, number, ct);
            collection.EpisodeId = episode.Id;
            collection.SeasonId = null;
        }
        else
        {
            collection.SeasonId = season.Id;
            collection.EpisodeId = null;
        }
    }

    /// <summary>Archive parts join the collection named like them; a video always starts its own.</summary>
    private static async Task<Collection> CollectionForAsync(CinegramDb db, FileUpload upload, CancellationToken ct)
    {
        var name = upload.Filename.Split('.')[0];
        var probe = upload.TechnicalMetadata;

        if (upload.MimeType?.StartsWith("video/") != true)
        {
            var existing = await db.Collections.OrderBy(c => c.Id).FirstOrDefaultAsync(c => c.Name == name, ct);
            if (existing is not null)
            {
                if (!string.IsNullOrEmpty(probe) && string.IsNullOrEmpty(existing.TechnicalMetadata))
                    existing.TechnicalMetadata = probe;

                if (!string.IsNullOrEmpty(existing.TechnicalMetadata))
                {
                    existing.Quality = CollectionMetadata.QualityFromProbe(existing.TechnicalMetadata) ?? existing.Quality;
                    existing.Codec = CollectionMetadata.CodecFromProbe(existing.TechnicalMetadata) ?? existing.Codec;
                }
                else if (string.IsNullOrEmpty(existing.Quality))
                {
                    existing.Quality = CollectionMetadata.QualityFromFilename(upload.Filename) ?? existing.Quality;
                }
                return existing;
            }
        }

        var collection = new Collection
        {
            Name = name,
            Quality = (string.IsNullOrEmpty(probe) ? null : CollectionMetadata.QualityFromProbe(probe))
                      ?? CollectionMetadata.QualityFromFilename(upload.Filename),
            Codec = string.IsNullOrEmpty(probe) ? null : CollectionMetadata.CodecFromProbe(probe),
            TechnicalMetadata = probe,
        };
        db.Collections.Add(collection);
        return collection;
    }

    private static UploadOutcome Outcome(TelegramFile file, Collection collection, bool duplicate) => new(
        file.Id, file.MessageId, file.Filename, collection.Id, collection.MovieId, collection.SeasonId, collection.EpisodeId,
        duplicate);

    private static IQueryable<Movie> MoviesWithFiles(CinegramDb db) =>
        db.Movies.AsNoTracking().AsSplitQuery()
            .Include(m => m.Collections.OrderBy(c => c.Id)).ThenInclude(c => c.Files.OrderBy(f => f.Id));

    private static IQueryable<Series> SeriesWithFiles(CinegramDb db) =>
        db.Series.AsNoTracking().AsSplitQuery()
            .Include(s => s.Seasons.OrderBy(se => se.Id)).ThenInclude(se => se.Collections.OrderBy(c => c.Id))
            .ThenInclude(c => c.Files.OrderBy(f => f.Id))
            .Include(s => s.Seasons).ThenInclude(se => se.Episodes.OrderBy(e => e.Id))
            .ThenInclude(e => e.Collections.OrderBy(c => c.Id)).ThenInclude(c => c.Files.OrderBy(f => f.Id));
}
