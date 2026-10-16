using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cinegram.Data.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Data;
using Cinegram.Tests.Data;
using Cinegram.Transfers;

namespace Cinegram.Tests.Transfers;

public class TransferTasksTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-transfers-test-").FullName;
    private IDbContextFactory<CinegramDb> _db = null!;
    private TransferTasks _tasks = null!;
    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private int _nextMessageId = 5000;

    public async Task InitializeAsync()
    {
        var path = Path.Combine(_dir, "database.db");
        CinegramDbTests.CreateBackendDatabase(path, Baseline.LegacyAlembicRevision);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContextFactory<CinegramDb>(o => o.UseSqlite(CinegramDb.ConnectionString(path)));
        builder.Services.AddSingleton<TransferCancellation>();
        builder.Services.AddSingleton<TransferTasks>();
        _app = builder.Build();
        TransferEndpoints.Map(_app);
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _http = new HttpClient { BaseAddress = new Uri(address) };
        _tasks = _app.Services.GetRequiredService<TransferTasks>();
        _db = _app.Services.GetRequiredService<IDbContextFactory<CinegramDb>>();
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task<T> Add<T>(T entity) where T : class
    {
        await using var db = await _db.CreateDbContextAsync();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private TelegramFile File(string filename = "Mugen.Train.2020.1080p.mkv", int? userMessageId = null) =>
        new() { MessageId = _nextMessageId++, UserMessageId = userMessageId, Filename = filename, Filesize = 1048576 };

    private async Task<Collection> MovieCollection(string title = "Mugen Train", string? quality = "1080p")
    {
        var movie = await Add(new Movie { TmdbId = 635302, Title = title, ReleaseYear = 2020, Overview = "Train." });
        return await Add(new Collection { MovieId = movie.Id, Quality = quality, Files = [File()] });
    }

    private async Task<(Series Series, Season Season, Episode Episode)> Show(int seasonNumber = 1, int episodeNumber = 1)
    {
        var series = await Add(new Series { TmdbId = 1429, TvdbId = 267440, ManualTitle = "Attack on Titan", ReleaseYear = 2013 });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = seasonNumber, LocalMetadata = true });
        var episode = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = episodeNumber, Title = "To You" });
        return (series, season, episode);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string?> Detail(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
    }

    private async Task<List<Cinegram.Data.DownloadTask>> DownloadRows()
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.DownloadTasks.OrderBy(t => t.Id).ToListAsync();
    }

    private async Task<Cinegram.Data.UploadTask> UploadRow(int id)
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.UploadTasks.SingleAsync(t => t.Id == id);
    }

    private Task<HttpResponseMessage> EnqueueUpload(string jellyfinId = "jf-1") =>
        _http.PostAsJsonAsync("/uploads/enqueue", new
        {
            jellyfin_id = jellyfinId, tmdb_id = 635302, media_type = "movie", path = "/media/movies/Mugen Train.mkv",
            title = "Mugen Train", year = 2020,
        });

    [Fact]
    public async Task EmptyQueues_ListNothingAndClearNothing()
    {
        Assert.Empty((await Json(await _http.GetAsync("/tasks/downloads"))).EnumerateArray());
        Assert.Empty((await Json(await _http.GetAsync("/tasks/uploads"))).EnumerateArray());

        var cleared = await Json(await _http.DeleteAsync("/tasks/completed"));
        Assert.Equal("ok", cleared.GetProperty("status").GetString());
        Assert.Equal(0, cleared.GetProperty("cleared_downloads").GetInt32());
        Assert.Equal(0, cleared.GetProperty("cleared_uploads").GetInt32());
    }

    [Fact]
    public async Task EnqueueCollection_StoresTheNameSuffix()
    {
        var collection = await MovieCollection();

        var body = await Json(await _http.PostAsJsonAsync(
            $"/downloads/enqueue/collection/{collection.Id}", new { name_suffix = "Erai BDRip" }));
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Enqueued collection", body.GetProperty("message").GetString());

        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        Assert.Equal("Erai BDRip", job.NameSuffix);
    }

    [Fact]
    public async Task EnqueueCollection_WorksWithoutABody()
    {
        var collection = await MovieCollection();

        await Json(await _http.PostAsync($"/downloads/enqueue/collection/{collection.Id}", null));

        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        Assert.Null(job.NameSuffix);
    }

    [Fact]
    public async Task EnqueueCollection_ReadsTheBodyWhateverItsContentType()
    {
        var first = await MovieCollection();
        var second = await Add(new Collection { MovieId = first.MovieId, Files = [File()] });

        await Json(await _http.PostAsync($"/downloads/enqueue/collection/{first.Id}",
            new FormUrlEncodedContent([])));
        await Json(await _http.PostAsync($"/downloads/enqueue/collection/{second.Id}",
            new StringContent("""{"name_suffix": "v2"}""", System.Text.Encoding.UTF8, "text/plain")));
        var invalid = await _http.PostAsync($"/downloads/enqueue/collection/{first.Id}",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal([null, "v2"], (await _tasks.ClaimPendingDownloadsAsync()).Select(j => j.NameSuffix));
    }

    [Fact]
    public async Task EnqueueCollection_IgnoresQueuedEmptyOrUnknownCollections()
    {
        var collection = await MovieCollection();
        var empty = await Add(new Collection { MovieId = collection.MovieId });
        await _tasks.EnqueueCollectionAsync(collection.Id);

        foreach (var id in new[] { collection.Id, empty.Id, 9999 })
        {
            var body = await Json(await _http.PostAsync($"/downloads/enqueue/collection/{id}", null));
            Assert.Equal("ignored", body.GetProperty("status").GetString());
            Assert.Equal("Collection already in queue or empty", body.GetProperty("message").GetString());
        }

        Assert.Single(await DownloadRows());
    }

    [Fact]
    public async Task EnqueueCollection_QueuesAgainOnceTheLastOneFinished()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var task = (await DownloadRows())[0];
        await _tasks.UpdateDownloadAsync(task.Id, "failed", 0, "boom");

        Assert.True(await _tasks.EnqueueCollectionAsync(collection.Id));
        Assert.Equal(2, (await DownloadRows()).Count);
    }

    [Fact]
    public async Task EnqueueSeries_QueuesEpisodeAndSeasonCollections()
    {
        var (series, season, episode) = await Show();
        var second = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 2 });
        await Add(new Collection { EpisodeId = episode.Id, Files = [File()] });
        await Add(new Collection { EpisodeId = second.Id, Files = [File()] });
        await Add(new Collection { SeasonId = season.Id, Files = [File()] });
        await Add(new Collection { EpisodeId = second.Id });

        var body = await Json(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}", null));
        Assert.Equal("Enqueued 3 collections for series", body.GetProperty("message").GetString());

        body = await Json(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}", null));
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Enqueued 0 collections for series", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task EnqueueSeason_QueuesOnlyThatSeason()
    {
        var (series, season, episode) = await Show();
        var other = await Add(new Season { SeriesId = series.Id, SeasonNumber = 2 });
        await Add(new Collection { EpisodeId = episode.Id, Files = [File()] });
        await Add(new Collection { SeasonId = other.Id, Files = [File()] });

        var body = await Json(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}/season/1", null));
        Assert.Equal("Enqueued 1 collections for season 1", body.GetProperty("message").GetString());
        Assert.Single(await DownloadRows());
    }

    [Fact]
    public async Task EnqueueEpisode_QueuesOrIgnores()
    {
        var (series, _, episode) = await Show();
        await Add(new Collection { EpisodeId = episode.Id, Files = [File()] });
        var url = $"/downloads/enqueue/series/{series.Id}/season/1/episode/1";

        var body = await Json(await _http.PostAsync(url, null));
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Enqueued episode", body.GetProperty("message").GetString());

        body = await Json(await _http.PostAsync(url, null));
        Assert.Equal("ignored", body.GetProperty("status").GetString());
        Assert.Equal("Episode collection already enqueued or not found", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task EnqueueBySeries_AnswersNotFound()
    {
        var (series, _, _) = await Show();

        Assert.Equal("Series not found", await Detail(await _http.PostAsync("/downloads/enqueue/series/9999", null), HttpStatusCode.NotFound));
        Assert.Equal("Series not found", await Detail(await _http.PostAsync("/downloads/enqueue/series/9999/season/1", null), HttpStatusCode.NotFound));
        Assert.Equal("Season not found", await Detail(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}/season/5", null), HttpStatusCode.NotFound));
        Assert.Equal("Season not found", await Detail(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}/season/5/episode/1", null), HttpStatusCode.NotFound));
        Assert.Equal("Episode not found", await Detail(await _http.PostAsync($"/downloads/enqueue/series/{series.Id}/season/1/episode/9", null), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ClaimPendingDownloads_DescribesAMovie()
    {
        var collection = await MovieCollection(quality: null);
        await _tasks.EnqueueCollectionAsync(collection.Id, "v2");

        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        Assert.Equal(collection.Id, job.CollectionId);
        Assert.Equal("movie", job.MediaType);
        Assert.Equal("Mugen Train", job.Title);
        Assert.Equal(2020, job.Year);
        Assert.Equal(635302, job.TmdbId);
        Assert.Equal("Train.", job.Overview);
        Assert.Equal("1080p", job.Quality);
        Assert.Null(job.SeasonNumber);
        Assert.False(job.LocalMetadata);

        var task = Assert.Single(await DownloadRows());
        Assert.Equal("downloading", task.Status);
        Assert.Empty(await _tasks.ClaimPendingDownloadsAsync());
    }

    [Fact]
    public async Task ClaimPendingDownloads_DescribesAnEpisodeAndASeasonPack()
    {
        var (_, season, episode) = await Show(seasonNumber: 3, episodeNumber: 7);
        await _tasks.EnqueueCollectionAsync((await Add(new Collection { EpisodeId = episode.Id, Quality = "720p", Files = [File()] })).Id);
        await _tasks.EnqueueCollectionAsync((await Add(new Collection { SeasonId = season.Id, Files = [File()] })).Id);

        var jobs = await _tasks.ClaimPendingDownloadsAsync();
        Assert.Equal(2, jobs.Count);
        foreach (var job in jobs)
        {
            Assert.Equal("tv", job.MediaType);
            Assert.Equal("Attack on Titan", job.Title);
            Assert.Equal(2013, job.Year);
            Assert.Equal(1429, job.TmdbId);
            Assert.Equal(267440, job.TvdbId);
            Assert.Equal(3, job.SeasonNumber);
            Assert.True(job.LocalMetadata);
        }

        Assert.Equal(7, jobs[0].EpisodeNumber);
        Assert.Equal("To You", jobs[0].EpisodeTitle);
        Assert.Equal("720p", jobs[0].Quality);
        Assert.Null(jobs[1].EpisodeNumber);
        Assert.Null(jobs[1].EpisodeTitle);
    }

    [Theory]
    [InlineData(91001, 91001)]
    [InlineData(null, null)]
    public async Task ClaimPendingDownloads_CarriesBothMessageIds(int? userMessageId, int? expected)
    {
        var movie = await Add(new Movie { Title = "Mugen Train" });
        var collection = await Add(new Collection { MovieId = movie.Id, Files = [File(userMessageId: userMessageId)] });
        await _tasks.EnqueueCollectionAsync(collection.Id);

        var file = Assert.Single(Assert.Single(await _tasks.ClaimPendingDownloadsAsync()).Files);
        Assert.Equal(collection.Files[0].MessageId, file.MessageId);
        Assert.Equal(expected, file.UserMessageId);
        Assert.Equal("bot", file.StoragePeer);
        Assert.Equal(1048576, file.Filesize);
    }

    [Fact]
    public async Task ClaimPendingDownloads_LeavesTasksWhoseCollectionIsGone()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        await using (var db = await _db.CreateDbContextAsync())
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await db.Files.ExecuteDeleteAsync();
            await db.Collections.ExecuteDeleteAsync();
        }

        Assert.Empty(await _tasks.ClaimPendingDownloadsAsync());
        Assert.Equal("pending", Assert.Single(await DownloadRows()).Status);
    }

    [Fact]
    public async Task Enqueue_WakesTheServices()
    {
        var collection = await MovieCollection();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _http.PostAsync($"/downloads/enqueue/collection/{collection.Id}", null);
        await _tasks.Downloads.WaitAsync(timeout.Token);

        await EnqueueUpload();
        await _tasks.Uploads.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task CompletedDownload_RecordsTheLocalPath()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id, "v2");
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());

        const string path = "/data/import/movies/Mugen Train (2020)/Mugen Train (2020) - [1080p v2].mkv";
        await _tasks.CompleteDownloadAsync(job.TaskId, collection.Id, path);

        await using var db = await _db.CreateDbContextAsync();
        Assert.Equal(path, (await db.Collections.SingleAsync()).LocalPath);
        var task = await db.DownloadTasks.SingleAsync();
        Assert.Equal("completed", task.Status);
        Assert.Equal(100, task.Progress);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public async Task A_download_cancelled_after_reaching_the_library_still_records_the_local_path()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        Assert.True(await _tasks.DeleteDownloadAsync(job.TaskId));

        await _tasks.CompleteDownloadAsync(job.TaskId, collection.Id, "/data/import/movies/x.mkv");

        await using var db = await _db.CreateDbContextAsync();
        Assert.Equal("/data/import/movies/x.mkv", (await db.Collections.SingleAsync()).LocalPath);
        Assert.Empty(await db.DownloadTasks.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_task_cancels_its_transfer_in_progress_and_tells_a_waiting_one_to_skip()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        var cancellation = _app.Services.GetRequiredService<TransferCancellation>();
        using var running = cancellation.Start(TransferKind.Download, job.TaskId);
        Assert.True(await _tasks.DownloadExistsAsync(job.TaskId));

        var response = await _http.DeleteAsync($"/downloads/{job.TaskId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(running.Token.IsCancellationRequested);
        Assert.False(await _tasks.DownloadExistsAsync(job.TaskId));
    }

    [Fact]
    public async Task Deleting_an_upload_cancels_its_transfer_in_progress()
    {
        await EnqueueUpload();
        var task = Assert.Single(await _tasks.ClaimPendingUploadsAsync());
        var cancellation = _app.Services.GetRequiredService<TransferCancellation>();
        using var running = cancellation.Start(TransferKind.Upload, task.Id);

        Assert.True(await _tasks.DeleteUploadAsync(task.Id));

        Assert.True(running.Token.IsCancellationRequested);
        Assert.False(await _tasks.UploadExistsAsync(task.Id));
    }

    [Fact]
    public async Task ProgressReports_LeaveTheLocalPathAndAFinishedTaskAlone()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());

        await _tasks.ReportDownloadProgressAsync(job.TaskId, 40);
        Assert.Equal(40, (await DownloadRows())[0].Progress);

        await _tasks.UpdateDownloadAsync(job.TaskId, "failed", 0, "boom");
        await _tasks.ReportDownloadProgressAsync(job.TaskId, 99);

        var task = (await DownloadRows())[0];
        Assert.Equal("failed", task.Status);
        Assert.Equal(0, task.Progress);
        Assert.Equal("boom", task.ErrorMessage);
        await using var db = await _db.CreateDbContextAsync();
        Assert.Null((await db.Collections.SingleAsync()).LocalPath);
    }

    [Fact]
    public async Task DownloadQueue_TitlesEachTaskAndHidesCompletedOnes()
    {
        var movie = await MovieCollection();
        var (_, season, episode) = await Show(seasonNumber: 2, episodeNumber: 5);
        var episodeCollection = await Add(new Collection { EpisodeId = episode.Id, Files = [File()] });
        var seasonCollection = await Add(new Collection { SeasonId = season.Id, Files = [File()] });
        var loose = await Add(new Collection { Name = "Loose file", Files = [File()] });
        foreach (var id in new[] { movie.Id, episodeCollection.Id, seasonCollection.Id, loose.Id })
            await _tasks.EnqueueCollectionAsync(id);
        var done = await Add(new Cinegram.Data.DownloadTask { CollectionId = loose.Id, Status = "completed" });

        var queue = (await Json(await _http.GetAsync("/downloads/queue"))).EnumerateArray().ToList();

        Assert.Equal(
            ["Mugen Train", "Attack on Titan - S2E5", "Attack on Titan - S2", "Loose file"],
            queue.Select(t => t.GetProperty("title").GetString()));
        Assert.DoesNotContain(queue, t => t.GetProperty("id").GetInt32() == done.Id);
        var first = queue[0];
        Assert.Equal(movie.Id, first.GetProperty("collection_id").GetInt32());
        Assert.Equal("pending", first.GetProperty("status").GetString());
        Assert.Equal(0, first.GetProperty("progress").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("error_message").ValueKind);
    }

    [Fact]
    public async Task DownloadTasks_ListEveryRowWithTheBackendFields()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id, "v2");

        var task = Assert.Single((await Json(await _http.GetAsync("/tasks/downloads"))).EnumerateArray());
        Assert.Equal(
            ["id", "collection_id", "name_suffix", "status", "progress", "error_message", "created_at", "completed_at"],
            task.EnumerateObject().Select(p => p.Name));
        Assert.Equal("v2", task.GetProperty("name_suffix").GetString());
    }

    [Fact]
    public async Task DeleteAndRetryDownload()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        await _tasks.UpdateDownloadAsync(job.TaskId, "failed", 0, "boom");

        Assert.Equal("ok", (await Json(await _http.PostAsync($"/downloads/{job.TaskId}/retry", null))).GetProperty("status").GetString());
        var task = (await DownloadRows())[0];
        Assert.Equal(("pending", 0, (string?)null), (task.Status, task.Progress, task.ErrorMessage));

        Assert.Equal("ok", (await Json(await _http.DeleteAsync($"/downloads/{job.TaskId}"))).GetProperty("status").GetString());
        Assert.Empty(await DownloadRows());

        Assert.Equal("Task not found", await Detail(await _http.DeleteAsync($"/downloads/{job.TaskId}"), HttpStatusCode.NotFound));
        Assert.Equal("Download task not found", await Detail(await _http.PostAsync($"/downloads/{job.TaskId}/retry", null), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task EnqueueUpload_IgnoresAnItemAlreadyQueued()
    {
        Assert.Equal("ok", (await Json(await EnqueueUpload())).GetProperty("status").GetString());

        var again = await Json(await EnqueueUpload());
        Assert.Equal("ignored", again.GetProperty("status").GetString());
        Assert.Equal("Already in upload queue", again.GetProperty("message").GetString());

        var task = Assert.Single(await _tasks.ClaimPendingUploadsAsync());
        Assert.Equal(("jf-1", 635302, "movie", "/media/movies/Mugen Train.mkv", "Mugen Train", 2020, "uploading"),
            (task.JellyfinId, task.TmdbId, task.MediaType, task.Path, task.Title, task.Year, task.Status));

        await _tasks.UpdateUploadAsync(task.Id, "completed", 100);
        Assert.Equal("ok", (await Json(await EnqueueUpload())).GetProperty("status").GetString());
    }

    [Fact]
    public async Task EnqueueUpload_RequiresTheItemFields()
    {
        var response = await _http.PostAsJsonAsync("/uploads/enqueue", new { jellyfin_id = "jf-1", media_type = "movie" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(await _tasks.ListUploadsAsync());
    }

    [Fact]
    public async Task UploadQueue_ListsActiveAndFailedUploads()
    {
        foreach (var id in new[] { "a", "b", "c" }) await EnqueueUpload(id);
        var rows = await _tasks.ListUploadsAsync();
        await _tasks.UpdateUploadAsync(rows[0].Id, "completed", 100);
        await _tasks.UpdateUploadAsync(rows[1].Id, "failed", 0, "boom");

        var queue = (await Json(await _http.GetAsync("/uploads/queue"))).EnumerateArray().ToList();

        Assert.Equal(["b", "c"], queue.Select(t => t.GetProperty("jellyfin_id").GetString()));
        Assert.Equal("boom", queue[0].GetProperty("error_message").GetString());
        Assert.Equal(
            ["id", "jellyfin_id", "tmdb_id", "media_type", "path", "title", "year", "status", "progress",
                "error_message", "created_at", "completed_at"],
            queue[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(3, (await Json(await _http.GetAsync("/tasks/uploads"))).GetArrayLength());
    }

    [Fact]
    public async Task UploadProgress_DoesNotUndoACompletion()
    {
        await EnqueueUpload();
        var task = Assert.Single(await _tasks.ClaimPendingUploadsAsync());

        await _tasks.ReportUploadProgressAsync(task.Id, 50);
        Assert.Equal(50, (await UploadRow(task.Id)).Progress);

        await _tasks.UpdateUploadAsync(task.Id, "completed", 100);
        await _tasks.ReportUploadProgressAsync(task.Id, 99);
        var row = await UploadRow(task.Id);
        Assert.Equal(("completed", 100), (row.Status, row.Progress));
        Assert.NotNull(row.CompletedAt);
    }

    [Fact]
    public async Task DeleteAndRetryUpload()
    {
        await EnqueueUpload();
        var task = Assert.Single(await _tasks.ClaimPendingUploadsAsync());
        await _tasks.UpdateUploadAsync(task.Id, "failed", 0, "boom");

        await Json(await _http.PostAsync($"/uploads/{task.Id}/retry", null));
        var row = await UploadRow(task.Id);
        Assert.Equal(("pending", 0, (string?)null), (row.Status, row.Progress, row.ErrorMessage));

        await Json(await _http.DeleteAsync($"/uploads/{task.Id}"));
        Assert.Equal("Task not found", await Detail(await _http.DeleteAsync($"/uploads/{task.Id}"), HttpStatusCode.NotFound));
        Assert.Equal("Upload task not found", await Detail(await _http.PostAsync($"/uploads/{task.Id}/retry", null), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ClearCompleted_RemovesOnlyCompletedTasks()
    {
        var collection = await MovieCollection();
        await Add(new Cinegram.Data.DownloadTask { CollectionId = collection.Id, Status = "completed" });
        await Add(new Cinegram.Data.DownloadTask { CollectionId = collection.Id, Status = "failed" });
        await EnqueueUpload("a");
        await EnqueueUpload("b");
        await _tasks.UpdateUploadAsync((await _tasks.ListUploadsAsync())[0].Id, "completed", 100);

        var cleared = await Json(await _http.DeleteAsync("/tasks/completed"));

        Assert.Equal(1, cleared.GetProperty("cleared_downloads").GetInt32());
        Assert.Equal(1, cleared.GetProperty("cleared_uploads").GetInt32());
        Assert.Equal("failed", Assert.Single(await DownloadRows()).Status);
        Assert.Equal("b", Assert.Single(await _tasks.ListUploadsAsync()).JellyfinId);
    }

    [Fact]
    public async Task RecoverInterrupted_RestartsDownloadsAndFailsUploads()
    {
        var collection = await MovieCollection();
        await _tasks.EnqueueCollectionAsync(collection.Id);
        var job = Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
        await _tasks.ReportDownloadProgressAsync(job.TaskId, 60);
        await EnqueueUpload("a");
        await EnqueueUpload("b");
        var upload = (await _tasks.ClaimPendingUploadsAsync())[0];
        await _tasks.UpdateUploadAsync((await _tasks.ListUploadsAsync())[1].Id, "completed", 100);

        Assert.Equal((1, 1), await _tasks.RecoverInterruptedAsync());

        var download = Assert.Single(await DownloadRows());
        Assert.Equal(("pending", 0), (download.Status, download.Progress));
        var failed = await UploadRow(upload.Id);
        Assert.Equal(("failed", TransferTasks.InterruptedUploadMessage), (failed.Status, failed.ErrorMessage));
        Assert.Equal("completed", (await _tasks.ListUploadsAsync())[1].Status);
        Assert.Single(await _tasks.ClaimPendingDownloadsAsync());
    }

    [Fact]
    public void Routes_KeepTheContractTheWebUses()
    {
        var registered = _app.Services.GetServices<EndpointDataSource>()
            .SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(m => $"{m} {e.RoutePattern.RawText}"))
            .ToHashSet();

        string[] contract =
        [
            "GET /tasks/downloads", "GET /tasks/uploads", "DELETE /tasks/completed",
            "POST /uploads/enqueue", "GET /uploads/queue", "DELETE /uploads/{taskId:int}", "POST /uploads/{taskId:int}/retry",
            "GET /downloads/queue", "DELETE /downloads/{taskId:int}", "POST /downloads/{taskId:int}/retry",
            "POST /downloads/enqueue/collection/{collectionId:int}",
            "POST /downloads/enqueue/series/{seriesId:int}",
            "POST /downloads/enqueue/series/{seriesId:int}/season/{seasonNumber:int}",
            "POST /downloads/enqueue/series/{seriesId:int}/season/{seasonNumber:int}/episode/{episodeNumber:int}",
        ];
        Assert.Empty(contract.Except(registered));
    }
}
