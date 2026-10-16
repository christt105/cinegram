using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Cinegram.Data;

namespace Cinegram.Transfers;

/// <summary>
/// Wakes a transfer service when work is queued. Wakes that arrive while it is busy collapse into
/// one, so it rereads the table once instead of once per enqueue.
/// </summary>
public sealed class TransferSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken ct) => await _channel.Reader.ReadAsync(ct);
}

/// <summary>
/// The download and upload queues (<c>downloadtask</c> and <c>uploadtask</c> rows). A download
/// goes pending → downloading → completed/failed, an upload pending → uploading → completed/failed.
/// The table is the queue: enqueueing writes a pending row and wakes the matching service.
/// </summary>
public class TransferTasks(IDbContextFactory<CinegramDb> dbFactory, TransferCancellation cancellation)
{
    public const string InterruptedUploadMessage =
        "Interrupted by a restart. Retry to upload it again; parts already sent stay in Telegram.";

    private static readonly string[] ActiveDownloadStatuses = ["pending", "downloading"];
    private static readonly string[] ActiveUploadStatuses = ["pending", "uploading"];
    private static readonly string[] QueueDownloadStatuses = ["pending", "downloading", "failed"];
    private static readonly string[] QueueUploadStatuses = ["pending", "uploading", "failed"];

    public TransferSignal Downloads { get; } = new();

    public TransferSignal Uploads { get; } = new();

    public sealed record DownloadTaskOut(
        int Id, int CollectionId, string? NameSuffix, string Status, int Progress, string? ErrorMessage,
        DateTime CreatedAt, DateTime? CompletedAt);

    public sealed record UploadRequest(
        string JellyfinId, int? TmdbId, string MediaType, string Path, string Title, int? Year);

    public enum EnqueueError { None, SeriesNotFound, SeasonNotFound, EpisodeNotFound }

    public sealed record EnqueueOutcome(int Count, EnqueueError Error = EnqueueError.None);

    public async Task<List<DownloadTaskOut>> ListDownloadsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.DownloadTasks.OrderBy(t => t.Id)
            .Select(t => new DownloadTaskOut(
                t.Id, t.CollectionId, t.NameSuffix, t.Status, t.Progress, t.ErrorMessage, t.CreatedAt, t.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<List<UploadTask>> ListUploadsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UploadTasks.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<(int Downloads, int Uploads)> ClearCompletedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var downloads = await db.DownloadTasks.Where(t => t.Status == "completed").ExecuteDeleteAsync(ct);
        var uploads = await db.UploadTasks.Where(t => t.Status == "completed").ExecuteDeleteAsync(ct);
        return (downloads, uploads);
    }

    /// <summary>False when the collection is unknown, has no files, or is already queued.</summary>
    public async Task<bool> EnqueueCollectionAsync(int collectionId, string? nameSuffix = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var queued = await EnqueueCollectionAsync(db, collectionId, nameSuffix, ct);
        if (queued) Downloads.Notify();
        return queued;
    }

    /// <summary>Every episode and season collection of the series.</summary>
    public async Task<EnqueueOutcome> EnqueueSeriesAsync(int seriesId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Series.AnyAsync(s => s.Id == seriesId, ct)) return new(0, EnqueueError.SeriesNotFound);

        var seasonIds = await db.Seasons.Where(s => s.SeriesId == seriesId).OrderBy(s => s.Id)
            .Select(s => s.Id).ToListAsync(ct);
        var count = 0;
        foreach (var seasonId in seasonIds)
            count += await EnqueueSeasonCollectionsAsync(db, seasonId, ct);
        return Notified(count);
    }

    public async Task<EnqueueOutcome> EnqueueSeasonAsync(int seriesId, int seasonNumber, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Series.AnyAsync(s => s.Id == seriesId, ct)) return new(0, EnqueueError.SeriesNotFound);

        var seasonId = await FindSeasonAsync(db, seriesId, seasonNumber, ct);
        if (seasonId is null) return new(0, EnqueueError.SeasonNotFound);

        return Notified(await EnqueueSeasonCollectionsAsync(db, seasonId.Value, ct));
    }

    public async Task<EnqueueOutcome> EnqueueEpisodeAsync(
        int seriesId, int seasonNumber, int episodeNumber, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Series.AnyAsync(s => s.Id == seriesId, ct)) return new(0, EnqueueError.SeriesNotFound);

        var seasonId = await FindSeasonAsync(db, seriesId, seasonNumber, ct);
        if (seasonId is null) return new(0, EnqueueError.SeasonNotFound);

        var episodeId = await db.Episodes
            .Where(e => e.SeasonId == seasonId && e.EpisodeNumber == episodeNumber)
            .OrderBy(e => e.Id).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        if (episodeId is null) return new(0, EnqueueError.EpisodeNotFound);

        return Notified(await EnqueueCollectionsAsync(db, db.Collections.Where(c => c.EpisodeId == episodeId), ct));
    }

    /// <summary>
    /// Marks the pending downloads as downloading and returns them with what the downloader needs
    /// to name and place the file. Downloads whose collection is gone stay pending, as before.
    /// </summary>
    public async Task<List<DownloadJob>> ClaimPendingDownloadsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tasks = await db.DownloadTasks
            .Include(t => t.Collection!).ThenInclude(c => c.Files)
            .Include(t => t.Collection!).ThenInclude(c => c.Movie)
            .Include(t => t.Collection!).ThenInclude(c => c.Episode!).ThenInclude(e => e.Season!).ThenInclude(s => s.Series)
            .Include(t => t.Collection!).ThenInclude(c => c.Season!).ThenInclude(s => s.Series)
            .Where(t => t.Status == "pending")
            .OrderBy(t => t.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        var jobs = new List<DownloadJob>();
        foreach (var task in tasks.Where(t => t.Collection is not null))
        {
            task.Status = "downloading";
            task.Progress = 0;
            jobs.Add(ToJob(task, task.Collection!));
        }

        await db.SaveChangesAsync(ct);
        return jobs;
    }

    /// <summary>Downloads (pending, downloading, failed) as the transfers view lists them.</summary>
    public async Task<List<QueueDownloadTask>> DownloadQueueAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tasks = await db.DownloadTasks.AsNoTracking()
            .Include(t => t.Collection!).ThenInclude(c => c.Movie)
            .Include(t => t.Collection!).ThenInclude(c => c.Episode!).ThenInclude(e => e.Season!).ThenInclude(s => s.Series)
            .Include(t => t.Collection!).ThenInclude(c => c.Season!).ThenInclude(s => s.Series)
            .Where(t => QueueDownloadStatuses.Contains(t.Status))
            .OrderBy(t => t.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        return tasks.Where(t => t.Collection is not null).Select(t => new QueueDownloadTask
        {
            Id = t.Id,
            CollectionId = t.CollectionId,
            Title = QueueTitle(t.Collection!),
            Status = t.Status,
            Progress = t.Progress,
            ErrorMessage = t.ErrorMessage,
        }).ToList();
    }

    /// <summary>
    /// Progress reports only touch a row that is still downloading, so a late one cannot undo its
    /// completion or failure.
    /// </summary>
    public async Task<bool> UpdateDownloadAsync(
        int taskId, string status, int progress, string? errorMessage = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var task = await db.DownloadTasks.FindAsync([taskId], ct);
        if (task is null) return false;

        Apply(task, status, progress, errorMessage);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReportDownloadProgressAsync(int taskId, int progress, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.DownloadTasks.Where(t => t.Id == taskId && t.Status == "downloading")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Progress, progress), ct);
    }

    /// <summary>Deletes the task and stops its download if one is in progress.</summary>
    public async Task<bool> DeleteDownloadAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.DownloadTasks.Where(t => t.Id == taskId).ExecuteDeleteAsync(ct) > 0;
        cancellation.Cancel(TransferKind.Download, taskId);
        return deleted;
    }

    /// <summary>False once the task was cancelled, so a claimed task still waiting its turn is skipped.</summary>
    public async Task<bool> DownloadExistsAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.DownloadTasks.AnyAsync(t => t.Id == taskId, ct);
    }

    /// <summary>
    /// Records where a download landed on its collection even if the task was cancelled after the
    /// file reached the library, so Cinegram never loses track of a file it put there.
    /// </summary>
    public async Task CompleteDownloadAsync(int taskId, int collectionId, string localPath, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.DownloadTasks.FindAsync([taskId], ct) is { } task)
            Apply(task, "completed", 100, null);
        if (await db.Collections.FindAsync([collectionId], ct) is { } collection)
            collection.LocalPath = localPath;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RetryDownloadAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var task = await db.DownloadTasks.FindAsync([taskId], ct);
        if (task is null) return false;

        Reset(task);
        await db.SaveChangesAsync(ct);
        Downloads.Notify();
        return true;
    }

    /// <summary>False when the Jellyfin item is already pending or uploading.</summary>
    public async Task<bool> EnqueueUploadAsync(UploadRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var queued = await db.UploadTasks.AnyAsync(
            t => t.JellyfinId == request.JellyfinId && ActiveUploadStatuses.Contains(t.Status), ct);
        if (queued) return false;

        db.UploadTasks.Add(new UploadTask
        {
            JellyfinId = request.JellyfinId,
            TmdbId = request.TmdbId,
            MediaType = request.MediaType,
            Path = request.Path,
            Title = request.Title,
            Year = request.Year,
        });
        await db.SaveChangesAsync(ct);
        Uploads.Notify();
        return true;
    }

    public async Task<List<UploadTask>> ClaimPendingUploadsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tasks = await db.UploadTasks.Where(t => t.Status == "pending").OrderBy(t => t.Id).ToListAsync(ct);
        foreach (var task in tasks)
        {
            task.Status = "uploading";
            task.Progress = 0;
        }

        await db.SaveChangesAsync(ct);
        return tasks;
    }

    public async Task<List<UploadTask>> UploadQueueAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UploadTasks.AsNoTracking()
            .Where(t => QueueUploadStatuses.Contains(t.Status)).OrderBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<bool> UpdateUploadAsync(
        int taskId, string status, int progress, string? errorMessage = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var task = await db.UploadTasks.FindAsync([taskId], ct);
        if (task is null) return false;

        task.Status = status;
        task.Progress = progress;
        if (!string.IsNullOrEmpty(errorMessage)) task.ErrorMessage = errorMessage;
        if (status is "completed" or "failed") task.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReportUploadProgressAsync(int taskId, int progress, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.UploadTasks.Where(t => t.Id == taskId && t.Status == "uploading")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Progress, progress), ct);
    }

    /// <summary>Deletes the task and stops its upload if one is in progress.</summary>
    public async Task<bool> DeleteUploadAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.UploadTasks.Where(t => t.Id == taskId).ExecuteDeleteAsync(ct) > 0;
        cancellation.Cancel(TransferKind.Upload, taskId);
        return deleted;
    }

    /// <summary>False once the task was cancelled, so a claimed task still waiting its turn is skipped.</summary>
    public async Task<bool> UploadExistsAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UploadTasks.AnyAsync(t => t.Id == taskId, ct);
    }

    public async Task<bool> RetryUploadAsync(int taskId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var task = await db.UploadTasks.FindAsync([taskId], ct);
        if (task is null) return false;

        task.Status = "pending";
        task.Progress = 0;
        task.ErrorMessage = null;
        await db.SaveChangesAsync(ct);
        Uploads.Notify();
        return true;
    }

    /// <summary>
    /// Only this process transfers, so on startup a row still downloading or uploading was cut off
    /// by the restart. A download starts over on its own; an upload is failed instead, as sending
    /// it again would duplicate the parts that already reached Telegram.
    /// </summary>
    public async Task<(int Downloads, int Uploads)> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var downloads = await db.DownloadTasks.Where(t => t.Status == "downloading")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "pending").SetProperty(t => t.Progress, 0), ct);
        var uploads = await db.UploadTasks.Where(t => t.Status == "uploading")
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "failed")
                .SetProperty(t => t.Progress, 0)
                .SetProperty(t => t.ErrorMessage, InterruptedUploadMessage)
                .SetProperty(t => t.CompletedAt, DateTime.UtcNow), ct);
        return (downloads, uploads);
    }

    private EnqueueOutcome Notified(int count)
    {
        if (count > 0) Downloads.Notify();
        return new(count);
    }

    private static Task<int?> FindSeasonAsync(CinegramDb db, int seriesId, int seasonNumber, CancellationToken ct) =>
        db.Seasons.Where(s => s.SeriesId == seriesId && s.SeasonNumber == seasonNumber)
            .OrderBy(s => s.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);

    private static async Task<int> EnqueueSeasonCollectionsAsync(CinegramDb db, int seasonId, CancellationToken ct)
    {
        var episodeIds = await db.Episodes.Where(e => e.SeasonId == seasonId).OrderBy(e => e.Id)
            .Select(e => e.Id).ToListAsync(ct);
        var count = 0;
        foreach (var episodeId in episodeIds)
            count += await EnqueueCollectionsAsync(db, db.Collections.Where(c => c.EpisodeId == episodeId), ct);
        return count + await EnqueueCollectionsAsync(db, db.Collections.Where(c => c.SeasonId == seasonId), ct);
    }

    private static async Task<int> EnqueueCollectionsAsync(CinegramDb db, IQueryable<Collection> collections, CancellationToken ct)
    {
        var ids = await collections.OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
        var count = 0;
        foreach (var id in ids)
            if (await EnqueueCollectionAsync(db, id, null, ct))
                count++;
        return count;
    }

    private static async Task<bool> EnqueueCollectionAsync(CinegramDb db, int collectionId, string? nameSuffix, CancellationToken ct)
    {
        if (!await db.Files.AnyAsync(f => f.CollectionId == collectionId, ct)) return false;

        var queued = await db.DownloadTasks.AnyAsync(
            t => t.CollectionId == collectionId && ActiveDownloadStatuses.Contains(t.Status), ct);
        if (queued) return false;

        db.DownloadTasks.Add(new DownloadTask
        {
            CollectionId = collectionId,
            NameSuffix = string.IsNullOrEmpty(nameSuffix) ? null : nameSuffix,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(DownloadTask task, string status, int progress, string? errorMessage)
    {
        task.Status = status;
        task.Progress = progress;
        if (!string.IsNullOrEmpty(errorMessage)) task.ErrorMessage = errorMessage;
        if (status is "completed" or "failed") task.CompletedAt = DateTime.UtcNow;
    }

    private static void Reset(DownloadTask task)
    {
        task.Status = "pending";
        task.Progress = 0;
        task.ErrorMessage = null;
    }

    private static DownloadJob ToJob(DownloadTask task, Collection collection)
    {
        var job = new DownloadJob
        {
            TaskId = task.Id,
            CollectionId = task.CollectionId,
            MediaType = "movie",
            Title = "Unknown",
            Quality = collection.Quality ?? "1080p",
            NameSuffix = task.NameSuffix,
            Files = collection.Files.OrderBy(f => f.Id).Select(f => new DownloadFileItem
            {
                Id = f.Id,
                MessageId = f.MessageId,
                UserMessageId = f.UserMessageId,
                Filename = f.Filename,
                Filesize = f.Filesize,
                StoragePeer = f.StoragePeer,
            }).ToArray(),
        };

        if (collection.MovieId is not null)
        {
            if (collection.Movie is { } movie)
            {
                job.Title = movie.Title!;
                job.Year = movie.ReleaseYear;
                job.TmdbId = movie.TmdbId;
                job.Overview = movie.Overview;
            }
        }
        else if (collection.EpisodeId is not null)
        {
            if (collection.Episode is { } episode)
            {
                job.MediaType = "tv";
                job.EpisodeNumber = episode.EpisodeNumber;
                job.EpisodeTitle = episode.Title;
                ApplySeason(job, episode.Season);
            }
        }
        else if (collection.SeasonId is not null && collection.Season is { } season)
        {
            job.MediaType = "tv";
            ApplySeason(job, season);
        }

        return job;
    }

    private static void ApplySeason(DownloadJob job, Season? season)
    {
        if (season is null) return;
        job.SeasonNumber = season.SeasonNumber;
        job.LocalMetadata = season.LocalMetadata;
        if (season.Series is not { } series) return;
        job.Title = series.ManualTitle!;
        job.Year = series.ReleaseYear;
        job.TmdbId = series.TmdbId;
        job.TvdbId = series.TvdbId;
        job.Overview = series.Overview;
    }

    private static string QueueTitle(Collection collection)
    {
        var title = collection.Name ?? "Unknown";
        if (collection.MovieId is not null)
            return collection.Movie?.Title ?? title;

        if (collection.EpisodeId is not null)
        {
            var episode = collection.Episode;
            return episode?.Season?.Series is { } series
                ? $"{series.ManualTitle ?? "Unknown Series"} - S{episode.Season.SeasonNumber}E{episode.EpisodeNumber}"
                : title;
        }

        if (collection.SeasonId is not null && collection.Season?.Series is { } seasonSeries)
            return $"{seasonSeries.ManualTitle ?? "Unknown Series"} - S{collection.Season.SeasonNumber}";

        return title;
    }
}
