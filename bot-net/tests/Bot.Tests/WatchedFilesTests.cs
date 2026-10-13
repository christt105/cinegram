using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bot.Data;
using Bot.Data.Migrations;
using Bot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bot.Tests;

public class WatchedFilesTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-watch-test-").FullName;
    private readonly StubLookup _tmdb = new();
    private WatchedFiles _store = null!;
    private WebApplication _app = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var path = Path.Combine(_dir, "database.db");
        CinegramDbTests.CreateBackendDatabase(path, Baseline.LegacyAlembicRevision);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContextFactory<CinegramDb>(o => o.UseSqlite(CinegramDb.ConnectionString(path)));
        builder.Services.AddSingleton<ITmdbLookup>(_tmdb);
        builder.Services.AddSingleton<WatchedFiles>();
        builder.Services.AddSingleton<BotHolder>();
        _app = builder.Build();
        WatchEndpoints.Map(_app);
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _http = new HttpClient { BaseAddress = new Uri(address) };
        _store = _app.Services.GetRequiredService<WatchedFiles>();
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task<WatchedFile> Report(string path, string filename = "Stub.Show.S01E02.mkv", long filesize = 123) =>
        (await _store.ReportAsync(path, filename, filesize)).Row;

    private async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<JsonElement>> List(string? status = null) =>
        (await Json(await _http.GetAsync(status is null ? "/watch" : $"/watch?status={status}")))
        .EnumerateArray().ToList();

    private Task<HttpResponseMessage> Resolve(int id, string action, object body) =>
        _http.PostAsJsonAsync($"/watch/{id}/{action}", body);

    [Fact]
    public async Task Report_RunsTheGuess()
    {
        var row = await Report("downloads/Stub.Show.S01E02.mkv");

        Assert.Equal("pending", row.Status);
        Assert.Equal(42, row.GuessTmdbId);
        Assert.Equal("Stub Show", row.GuessTitle);
        Assert.Equal("tv", row.GuessMediaType);
        Assert.Equal(2020, row.GuessYear);
        Assert.Equal((1, 2), (row.GuessSeason, row.GuessEpisode));
        Assert.Equal(0.9, row.Confidence);
        Assert.Equal("tmdb", row.GuessSource);
    }

    [Fact]
    public async Task Report_IsIdempotentPerPath()
    {
        var (first, firstCreated) = await _store.ReportAsync("downloads/dupe.mkv", "Dupe.mkv", 1);
        var (second, secondCreated) = await _store.ReportAsync("downloads/dupe.mkv", "Dupe.mkv", 1);

        Assert.Equal(first.Id, second.Id);
        Assert.True(firstCreated);
        Assert.False(secondCreated);
    }

    [Fact]
    public async Task Rename_ReguessesAPendingRow()
    {
        await Report("downloads/old.mkv", "Old.Name.mkv");

        var row = await _store.RenameAsync("downloads/old.mkv", "downloads/Stub.Show.S01E03.mkv");

        Assert.Equal("downloads/Stub.Show.S01E03.mkv", row!.Path);
        Assert.Equal("Stub.Show.S01E03.mkv", row.Filename);
        Assert.Equal(42, row.GuessTmdbId);
        Assert.Equal(3, row.GuessEpisode);
    }

    [Fact]
    public async Task Rename_DoesNotClobberAConfirmedRow()
    {
        var created = await Report("downloads/confirm-me.mkv", "Confirm.Me.mkv");
        await _store.ResolveAsync(created.Id, 42, null, null, "confirmed");

        var row = await _store.RenameAsync("downloads/confirm-me.mkv", "downloads/renamed-after-confirm.mkv");

        Assert.Equal("confirmed", row!.Status);
        Assert.Equal(42, row.GuessTmdbId);
    }

    [Fact]
    public async Task Rename_OfAnUntrackedPathIsNull() =>
        Assert.Null(await _store.RenameAsync("downloads/nope.mkv", "downloads/still-nope.mkv"));

    [Fact]
    public async Task MarkMissing_MarksTheRowRemovedWithoutDeletingIt()
    {
        var created = await Report("downloads/gone.mkv", "Gone.mkv");

        var row = await _store.MarkMissingAsync("downloads/gone.mkv");

        Assert.Equal("removed", row!.Status);
        Assert.Contains(await List("removed"), f => f.GetProperty("id").GetInt32() == created.Id);
    }

    [Fact]
    public async Task MarkMissing_IsANoOpForAnAlreadyMovedRow()
    {
        var created = await Report("downloads/moved.mkv", "Moved.mkv");
        await _store.MarkMovedAsync(created.Id, "/library/Moved.mkv");

        var row = await _store.MarkMissingAsync("downloads/moved.mkv");

        Assert.Equal("moved", row!.Status);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        await Report("downloads/a.mkv", "A.mkv");
        var b = await Report("downloads/b.mkv", "B.mkv");
        await Resolve(b.Id, "confirm", new { tmdb_id = 42 });

        var pending = await List("pending");
        var confirmed = await List("confirmed");

        Assert.All(pending, f => Assert.Equal("pending", f.GetProperty("status").GetString()));
        Assert.All(confirmed, f => Assert.Equal("confirmed", f.GetProperty("status").GetString()));
        Assert.Contains(confirmed, f => f.GetProperty("id").GetInt32() == b.Id);
    }

    [Fact]
    public async Task List_IsNewestFirstWithTheBackendFieldNames()
    {
        await Report("downloads/older.mkv", "Older.mkv");
        await Task.Delay(5);
        var newer = await Report("downloads/Stub.Show.S01E02.mkv");

        var all = await List();

        Assert.Equal(newer.Id, all[0].GetProperty("id").GetInt32());
        string[] fields =
        [
            "id", "path", "filename", "filesize", "first_seen_at", "guess_media_type", "guess_tmdb_id",
            "guess_tvdb_id", "guess_title", "guess_year", "guess_season", "guess_episode", "confidence",
            "guess_source", "status", "notified_at", "moved_path", "error_message",
        ];
        Assert.Equal(fields.Order(), all[0].EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Confirm_ReturnsTheFinalIdentityAndUpdatesTheStatus()
    {
        var created = await Report("downloads/confirm2.mkv", "Confirm2.mkv");

        var data = await Json(await Resolve(created.Id, "confirm", new { tmdb_id = 42 }));

        Assert.Equal("confirmed", data.GetProperty("status").GetString());
        Assert.Equal(42, data.GetProperty("tmdb_id").GetInt32());
        Assert.Equal("tv", data.GetProperty("media_type").GetString());
        Assert.Equal("Stub Show", data.GetProperty("title").GetString());
        Assert.Equal(2020, data.GetProperty("year").GetInt32());
        Assert.Equal("downloads/confirm2.mkv", data.GetProperty("path").GetString());
    }

    /// <summary>A show confirmed through /watch is written to disk under a [tvdbid-x] folder tag,
    /// which Jellyfin's TV providers honor more reliably than [tmdbid-x].</summary>
    [Fact]
    public async Task Confirm_OfAShowAlsoResolvesItsTvdbId()
    {
        var created = await Report("downloads/confirm-tvdb.mkv", "Confirm.Tvdb.mkv");

        var data = await Json(await Resolve(created.Id, "confirm", new { tmdb_id = 42 }));

        Assert.Equal(9999, data.GetProperty("tvdb_id").GetInt32());
        var row = (await List("confirmed")).Single(f => f.GetProperty("id").GetInt32() == created.Id);
        Assert.Equal(9999, row.GetProperty("guess_tvdb_id").GetInt32());
    }

    [Fact]
    public async Task Correct_OverridesTheTmdbIdAndSeasonEpisode()
    {
        var created = await Report("downloads/correct-me.mkv", "Correct.Me.mkv");

        var data = await Json(await Resolve(created.Id, "correct", new { tmdb_id = 100, season = 2, episode = 5 }));

        Assert.Equal("corrected", data.GetProperty("status").GetString());
        Assert.Equal(100, data.GetProperty("tmdb_id").GetInt32());
        Assert.Equal("Corrected Movie", data.GetProperty("title").GetString());
        Assert.Equal(2019, data.GetProperty("year").GetInt32());
        Assert.Equal((2, 5), (data.GetProperty("season").GetInt32(), data.GetProperty("episode").GetInt32()));
        Assert.Equal(JsonValueKind.Null, data.GetProperty("tvdb_id").ValueKind);
    }

    [Fact]
    public async Task Confirm_WithAnUnknownTmdbIdIsA404()
    {
        var created = await Report("downloads/unknown-id.mkv", "Unknown.Id.mkv");

        var response = await Resolve(created.Id, "confirm", new { tmdb_id = 999999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_OfAnUnknownRowIsA404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Resolve(12345, "confirm", new { tmdb_id = 42 })).StatusCode);

    [Fact]
    public async Task Confirm_WithoutATmdbIdIsA422()
    {
        var created = await Report("downloads/no-id.mkv", "No.Id.mkv");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Resolve(created.Id, "confirm", new { season = 1 })).StatusCode);
    }

    [Fact]
    public async Task Confirm_WhenTmdbFailsIsA502()
    {
        var created = await Report("downloads/tmdb-down.mkv", "Tmdb.Down.mkv");
        _tmdb.FailLookups = true;

        Assert.Equal(HttpStatusCode.BadGateway, (await Resolve(created.Id, "confirm", new { tmdb_id = 42 })).StatusCode);
    }

    [Fact]
    public async Task MarkNotified_SetsNotifiedAt()
    {
        var created = await Report("downloads/patch-notified.mkv", "Patch.Notified.mkv");
        Assert.Null(created.NotifiedAt);

        await _store.MarkNotifiedAsync(created.Id);

        var row = await _store.GetAsync(created.Id);
        Assert.Equal("notified", row!.Status);
        Assert.NotNull(row.NotifiedAt);
    }

    [Fact]
    public async Task MarkMoved_RecordsTheDestination()
    {
        var created = await Report("downloads/patch-moved.mkv", "Patch.Moved.mkv");

        await _store.MarkMovedAsync(created.Id, "/library/Show/S01E02.mkv");

        var row = await _store.GetAsync(created.Id);
        Assert.Equal(("moved", "/library/Show/S01E02.mkv"), (row!.Status, row.MovedPath));
    }

    [Fact]
    public async Task MarkError_RecordsTheMessage()
    {
        var created = await Report("downloads/patch-error.mkv", "Patch.Error.mkv");

        await _store.MarkErrorAsync(created.Id, "disk full");

        var row = await _store.GetAsync(created.Id);
        Assert.Equal(("error", "disk full"), (row!.Status, row.ErrorMessage));
    }

    [Fact]
    public async Task Reidentify_ReguessesOnlyUnresolvedRows()
    {
        var unresolved = await Report("downloads/unmatched.mkv", "Unmatched.File.mkv");
        Assert.Null(unresolved.GuessTmdbId);
        var confirmed = await Report("downloads/already-confirmed.mkv", "Confirm.Me.mkv");
        await Resolve(confirmed.Id, "confirm", new { tmdb_id = 42 });

        _tmdb.OnFilename = filename => filename.Contains("Unmatched")
            ? new TmdbTitle(42, "tv", Name: "Stub Show") { MatchScore = 0.9 }
            : null;
        var data = (await Json(await _http.PostAsync("/watch/reidentify", null))).EnumerateArray().ToList();

        var updated = data.Single(f => f.GetProperty("id").GetInt32() == unresolved.Id);
        Assert.Equal(42, updated.GetProperty("guess_tmdb_id").GetInt32());
        Assert.DoesNotContain(data, f => f.GetProperty("id").GetInt32() == confirmed.Id);
        Assert.Contains(await List("confirmed"), f => f.GetProperty("id").GetInt32() == confirmed.Id);
    }

    [Fact]
    public async Task Guess_AnExplicitTmdbTagIsFullConfidenceAndSourcedFromTheFilename()
    {
        _tmdb.OnFilename = _ => TmdbIdentificationTests.Movie(1292585, "The Boy and the Heron", releaseDate: "2023-07-14");

        var guess = await _store.GuessAsync("Hayao Miyazaki and the Heron (2024) [tmdbid-1292585].zip.001");

        Assert.Equal(new WatchedFiles.Guess("movie", 1292585, "The Boy and the Heron", 2023, null, null, 1.0, "filename"), guess);
    }

    [Fact]
    public async Task Guess_AFuzzyMatchConfidenceIsTheSimilarityScore()
    {
        _tmdb.OnFilename = _ => new TmdbTitle(999, "tv", Name: "Some Show", FirstAirDate: "2018-11-02") { MatchScore = 0.73 };

        var guess = await _store.GuessAsync("Some.Show.S01E02.mkv");

        Assert.Equal(new WatchedFiles.Guess("tv", 999, "Some Show", 2018, 1, 2, 0.73, "tmdb"), guess);
    }

    [Fact]
    public async Task Guess_NoMatchIsZeroConfidence()
    {
        _tmdb.OnFilename = _ => null;

        var guess = await _store.GuessAsync("Totally Unknown Thing.mkv");

        Assert.Equal(new WatchedFiles.Guess("movie", null, null, null, null, null, 0.0, "tmdb"), guess);
    }

    [Fact]
    public async Task Guess_TmdbErrorsAreSwallowedIntoAZeroConfidenceGuess()
    {
        _tmdb.OnFilename = _ => throw new HttpRequestException("network is down");

        var guess = await _store.GuessAsync("Whatever.mkv");

        Assert.Equal((0.0, (int?)null), (guess.Confidence, guess.TmdbId));
    }

    private class StubLookup : ITmdbLookup
    {
        public Func<string, TmdbTitle?>? OnFilename { get; set; }
        public bool FailLookups { get; set; }

        public Task<TmdbTitle?> IdentifyByFilenameAsync(string filename, CancellationToken ct = default)
        {
            if (OnFilename is not null) return Task.FromResult(OnFilename(filename));
            if (filename.Contains("S01E02") || filename.Contains("S01E03"))
                return Task.FromResult<TmdbTitle?>(
                    new TmdbTitle(42, "tv", Name: "Stub Show", FirstAirDate: "2020-05-01") { MatchScore = 0.9 });
            if (filename.Contains("[tmdbid-777]"))
                return Task.FromResult<TmdbTitle?>(new TmdbTitle(777, "movie", Title: "Stub Movie"));
            return Task.FromResult<TmdbTitle?>(null);
        }

        public Task<TmdbTitle?> IdentifyByTmdbIdAsync(int tmdbId, string? mediaTypeHint, CancellationToken ct = default)
        {
            if (FailLookups) throw new HttpRequestException("TMDB is down");
            return Task.FromResult(tmdbId switch
            {
                42 => new TmdbTitle(42, "tv", Name: "Stub Show", FirstAirDate: "2020-05-01"),
                100 => new TmdbTitle(100, "movie", Title: "Corrected Movie", ReleaseDate: "2019-03-01"),
                _ => null,
            });
        }

        public Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default) =>
            Task.FromResult<int?>(tvId == 42 ? 9999 : null);

        public Task<TmdbTitle?> GetAsync(string mediaType, int tmdbId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbSearchHit>> SearchAsync(string query, string kind, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int tmdbId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
