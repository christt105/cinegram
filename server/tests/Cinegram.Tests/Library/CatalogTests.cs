using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cinegram.Data.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Backups;
using Cinegram.Data;
using Cinegram.Library;
using Cinegram.Tests.Data;
using Cinegram.Tmdb;
using Cinegram.Transfers;

namespace Cinegram.Tests.Library;

public class CatalogTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-catalog-test-").FullName;
    private readonly FakeTmdb _tmdb = new();
    private IDbContextFactory<CinegramDb> _db = null!;
    private Catalog _catalog = null!;
    private CatalogIdentifier _identifier = null!;
    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private int _nextMessageId = 7000;

    public async Task InitializeAsync()
    {
        var path = Path.Combine(_dir, "database.db");
        CinegramDbTests.CreateBackendDatabase(path, Baseline.LegacyAlembicRevision);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContextFactory<CinegramDb>(o => o.UseSqlite(CinegramDb.ConnectionString(path)));
        builder.Services.AddSingleton<ITmdbLookup>(new TmdbIdentifier(_tmdb));
        builder.Services.AddSingleton<CatalogIdentifier>();
        builder.Services.AddSingleton<Catalog>();
        builder.Services.AddSingleton<DatabaseBackup>();
        _app = builder.Build();
        CatalogEndpoints.Map(_app);
        MaintenanceEndpoints.Map(_app);
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _http = new HttpClient { BaseAddress = new Uri(address) };
        _catalog = _app.Services.GetRequiredService<Catalog>();
        _identifier = _app.Services.GetRequiredService<CatalogIdentifier>();
        _db = _app.Services.GetRequiredService<IDbContextFactory<CinegramDb>>();
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private static TmdbTitle Movie(int id, string title, string? date = null) =>
        new(id, "movie", Title: title, ReleaseDate: date, PosterPath: $"/m{id}.jpg", Overview: $"About {title}.");

    private static TmdbTitle Show(int id, string name, string? date = null) =>
        new(id, "tv", Name: name, FirstAirDate: date, PosterPath: $"/s{id}.jpg", Overview: $"About {name}.");

    private Task<Catalog.UploadOutcome> Upload(
        string filename, string mimeType = "video/x-matroska", int? messageId = null, long? documentId = null,
        int? userMessageId = null, int? tmdbId = null, string? fwdFromType = null, string? fwdFromId = null,
        string? fwdFromName = null, bool fwdFromHidden = false, string? probe = null) =>
        _catalog.AddFileAsync(new Catalog.FileUpload(
            messageId ?? _nextMessageId++, filename, 1048576, mimeType, new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc),
            userMessageId, tmdbId, probe, "bot", documentId, fwdFromType, fwdFromId, fwdFromName, fwdFromHidden));

    private async Task<T> Add<T>(T entity) where T : class
    {
        await using var db = await _db.CreateDbContextAsync();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private async Task<Collection> Row(int collectionId)
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.Collections.Include(c => c.Files).Include(c => c.Episode).ThenInclude(e => e!.Season)
            .Include(c => c.Season).SingleAsync(c => c.Id == collectionId);
    }

    private async Task<TelegramFile> FileRow(int messageId)
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.Files.SingleAsync(f => f.MessageId == messageId);
    }

    private async Task<int> Count<T>() where T : class
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.Set<T>().CountAsync();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> Detail(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
    }

    private Task<HttpResponseMessage> Patch(string url, object body) => _http.PatchAsJsonAsync(url, body);

    // Uploads

    [Fact]
    public async Task A_video_upload_is_identified_and_linked()
    {
        _tmdb.Add(Movie(635302, "Mugen Train", "2020-10-16"), searchAs: "Mugen Train");

        var result = await Upload("Mugen Train (2020) 1080p.mkv");

        Assert.False(result.Duplicate);
        Assert.NotNull(result.MovieId);
        var collection = await Row(result.CollectionId);
        Assert.Equal("Mugen Train (2020) 1080p", collection.Name);
        Assert.Equal("1080p", collection.Quality);
        var movie = (await _catalog.GetMovieAsync(result.MovieId!.Value))!;
        Assert.Equal((635302, 2020, "/m635302.jpg", "About Mugen Train."),
            (movie.TmdbId!.Value, movie.ReleaseYear!.Value, movie.PosterPath, movie.Overview));
    }

    [Fact]
    public async Task An_upload_nothing_matches_stays_an_orphan()
    {
        var result = await Upload("zzqx-nothing.mkv");

        Assert.Equal((null, null, null), (result.MovieId, result.SeasonId, result.EpisodeId));
        Assert.Single(await _catalog.ListOrphansAsync());
    }

    [Fact]
    public async Task An_upload_with_a_tmdb_id_skips_the_guess()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));

        var result = await Upload("whatever.mkv", tmdbId: 129);

        Assert.Equal(129, (await _catalog.GetMovieAsync(result.MovieId!.Value))!.TmdbId);
    }

    [Fact]
    public async Task Archive_parts_share_a_collection_and_videos_do_not()
    {
        var first = await Upload("Postres.zip.001", "application/zip");
        var second = await Upload("Postres.zip.002", "application/zip");
        var videoA = await Upload("Postres.mkv");
        var videoB = await Upload("Postres.mkv");

        Assert.Equal(first.CollectionId, second.CollectionId);
        Assert.NotEqual(videoA.CollectionId, videoB.CollectionId);
        Assert.Equal(2, (await Row(first.CollectionId)).Files.Count);
    }

    [Fact]
    public async Task Probed_metadata_sets_quality_and_codec_on_upload()
    {
        var result = await Upload("Movie.mkv", probe: CollectionMetadataTests.Probe(3840, 2160, "hevc", "smpte2084"));

        var collection = await Row(result.CollectionId);
        Assert.Equal(("4K HDR", "H.265/HEVC"), (collection.Quality, collection.Codec));
    }

    [Fact]
    public async Task A_later_archive_part_with_a_probe_fills_in_the_collection()
    {
        var first = await Upload("Postres.zip.001", "application/zip");
        await Upload("Postres.zip.002", "application/zip", probe: CollectionMetadataTests.Probe(1920, 1080, "h264"));

        var collection = await Row(first.CollectionId);
        Assert.Equal(("1080p", "H.264/AVC"), (collection.Quality, collection.Codec));
        Assert.NotNull(collection.TechnicalMetadata);
    }

    [Fact]
    public async Task The_first_upload_of_a_document_is_not_a_duplicate() =>
        Assert.False((await Upload("Postres.zip.001", "application/zip", documentId: 111)).Duplicate);

    [Fact]
    public async Task A_reforward_of_the_same_document_is_a_duplicate_in_the_same_collection()
    {
        var first = await Upload("Postres.zip.001", "application/zip", documentId: 222);

        var again = await Upload("Postres.zip.001", "application/zip", documentId: 222);

        Assert.True(again.Duplicate);
        Assert.Equal(first.CollectionId, again.CollectionId);
        Assert.Equal(first.FileId, again.FileId);
    }

    [Fact]
    public async Task A_duplicate_forward_adds_no_file()
    {
        var first = await Upload("Postres.zip.001", "application/zip", documentId: 333);
        await Upload("Postres.zip.002", "application/zip", documentId: 334);
        await Upload("Postres.zip.001", "application/zip", documentId: 333);
        await Upload("Postres.zip.002", "application/zip", documentId: 334);

        Assert.Equal(2, (await Row(first.CollectionId)).Files.Count);
    }

    [Fact]
    public async Task Uploads_without_a_document_id_are_never_duplicates()
    {
        await Upload("Postres.zip.001", "application/zip");
        Assert.False((await Upload("Postres.zip.001", "application/zip")).Duplicate);
    }

    [Fact]
    public async Task An_upload_stores_both_message_ids()
    {
        await Upload("Mugen.Train.mkv", messageId: 6001, userMessageId: 91001);

        var file = await FileRow(6001);
        Assert.Equal((6001, 91001), (file.MessageId, file.UserMessageId!.Value));
    }

    [Fact]
    public async Task Uploading_the_same_message_again_fills_in_the_user_id_but_never_overwrites_it()
    {
        await Upload("Mugen.Train.mkv", messageId: 6003);
        await Upload("Mugen.Train.mkv", messageId: 6003, userMessageId: 91003);
        await Upload("Mugen.Train.mkv", messageId: 6003, userMessageId: 99999);

        Assert.Equal(91003, (await FileRow(6003)).UserMessageId);
        Assert.Equal(1, await Count<TelegramFile>());
    }

    [Fact]
    public async Task An_upload_stores_the_forward_origin()
    {
        await Upload("Frankenstein.mkv", messageId: 7001, documentId: 5794420050676948545, fwdFromType: "chat",
            fwdFromId: "-1001234567890", fwdFromName: "Las Cositas 3: La venganza");
        await Upload("Frankenstein.4K.mkv", messageId: 7002);
        await Upload("Frankenstein.HDR.mkv", messageId: 7003, documentId: 5798571127978597543, fwdFromType: "hidden_user",
            fwdFromName: "Someone", fwdFromHidden: true);

        var forwarded = await FileRow(7001);
        Assert.Equal((5794420050676948545L, "chat", "-1001234567890", "Las Cositas 3: La venganza", false),
            (forwarded.DocumentId!.Value, forwarded.FwdFromType, forwarded.FwdFromId, forwarded.FwdFromName, forwarded.FwdFromHidden));
        var plain = await FileRow(7002);
        Assert.Equal((null, null, false), (plain.DocumentId, plain.FwdFromType, plain.FwdFromHidden));
        var hidden = await FileRow(7003);
        Assert.Equal(("hidden_user", null, "Someone", true), (hidden.FwdFromType, hidden.FwdFromId, hidden.FwdFromName, hidden.FwdFromHidden));
    }

    [Fact]
    public async Task The_first_forward_origin_is_filled_in_once_and_kept()
    {
        await Upload("Movie.mkv", messageId: 7004);
        await Upload("Movie.mkv", messageId: 7004, documentId: 222, fwdFromType: "channel", fwdFromId: "-100999",
            fwdFromName: "Doraemon [Castellano]");
        await Upload("Movie.mkv", messageId: 7004, documentId: 333, fwdFromType: "chat", fwdFromId: "-100111",
            fwdFromName: "Las Cositas 3");

        var file = await FileRow(7004);
        Assert.Equal((222L, "channel"), (file.DocumentId!.Value, file.FwdFromType));
    }

    // Identification

    [Fact]
    public async Task An_episode_is_linked_to_its_series_season_and_episode()
    {
        _tmdb.Add(Show(37854, "One Piece", "1999-10-20"), searchAs: "One Piece");
        _tmdb.TvdbIds[37854] = 81797;

        var result = await Upload("One Piece 1x125.mkv");

        var collection = await Row(result.CollectionId);
        Assert.Equal((125, 1, "Episode 125"),
            (collection.Episode!.EpisodeNumber, collection.Episode.Season!.SeasonNumber, collection.Episode.Title));
        var series = (await _catalog.GetSeriesAsync(collection.Episode.Season.SeriesId))!;
        Assert.Equal((37854, 81797, "One Piece", 1999), (series.TmdbId!.Value, series.TvdbId!.Value, series.ManualTitle, series.ReleaseYear!.Value));
    }

    [Fact]
    public async Task A_forced_season_and_episode_place_an_ova()
    {
        _tmdb.Add(Show(67075, "Mob Psycho 100"));
        var collection = await Add(new Collection { Name = "Mob Psycho 100 II - OVA" });

        Assert.True(await _identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request(67075, "tv", 0, 1)));

        var row = await Row(collection.Id);
        Assert.Equal((null, 1, 0), (row.SeasonId, row.Episode!.EpisodeNumber, row.Episode.Season!.SeasonNumber));
    }

    [Fact]
    public async Task A_forced_season_without_episode_is_a_season_pack()
    {
        _tmdb.Add(Show(67075, "Mob Psycho 100"));
        var collection = await Add(new Collection { Name = "Mob Psycho 100 II - OVA" });

        await _identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request(67075, "tv", 0));

        var row = await Row(collection.Id);
        Assert.Equal((null, 0), (row.EpisodeId, row.Season!.SeasonNumber));
    }

    [Fact]
    public async Task Choosing_a_series_for_a_name_without_episode_markers_links_the_series()
    {
        _tmdb.Add(Movie(3557, "Leroy räumt auf"));
        _tmdb.Add(Show(3557, "Caminando bien"));
        var orphan = (await Upload("caminando bien.7z.001", "application/x-7z-compressed")).CollectionId;

        var response = await _http.PostAsJsonAsync($"/collections/{orphan}/identify", new { tmdb_id = 3557, media_type = "tv" });

        var body = await Json(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("movie_id").ValueKind);
        Assert.Equal(1, (await Row(orphan)).Season!.SeasonNumber);
        Assert.Empty(await _catalog.ListMoviesAsync());
    }

    [Fact]
    public async Task Without_a_chosen_type_the_name_decides_which_kind_is_tried_first()
    {
        _tmdb.Add(Movie(219937, "Como en una pesadilla"));
        _tmdb.Add(Show(219937, "Dexter: Pecado original"));
        var orphan = await Add(new Collection { Name = "Dexter Pecado Original 1X03 (WEB-DL1080p x265)" });

        await Json(await _http.PostAsJsonAsync($"/collections/{orphan.Id}/identify", new { tmdb_id = 219937 }));

        var row = await Row(orphan.Id);
        Assert.Equal((3, 1), (row.Episode!.EpisodeNumber, row.Episode.Season!.SeasonNumber));
    }

    [Fact]
    public async Task Identifying_with_an_unknown_tmdb_id_fails_and_leaves_the_orphan()
    {
        var orphan = await Add(new Collection { Name = "Something" });

        var detail = await Detail(await _http.PostAsJsonAsync($"/collections/{orphan.Id}/identify", new { tmdb_id = 999 }),
            HttpStatusCode.NotFound);

        Assert.Equal("TMDB ID 999 not found on TMDB", detail);
        Assert.Null((await Row(orphan.Id)).MovieId);
    }

    [Fact]
    public async Task Identifying_a_missing_collection_is_a_404() =>
        Assert.Equal("Collection not found",
            await Detail(await _http.PostAsJsonAsync("/collections/999/identify", new { tmdb_id = 1 }), HttpStatusCode.NotFound));

    [Fact]
    public async Task Identifying_one_collection_links_its_unidentified_siblings()
    {
        _tmdb.Add(Show(1396, "Breaking Bad"));
        var first = await Add(new Collection { Name = "Breaking Bad S01E01" });
        var second = await Add(new Collection { Name = "Breaking.Bad.S01E02.720p" });
        var other = await Add(new Collection { Name = "Better Call Saul S01E01" });

        await _identifier.IdentifyAsync(first.Id, new CatalogIdentifier.Request(1396, "tv"));

        Assert.Equal(2, (await Row(second.Id)).Episode!.EpisodeNumber);
        Assert.Single(_tmdb.Lookups);
        Assert.Equal(1, _tmdb.Lookups.Count);
    }

    [Fact]
    public async Task Siblings_with_a_different_year_are_not_linked()
    {
        _tmdb.Add(Movie(438631, "Dune", "2021-09-15"));
        var recent = await Add(new Collection { Name = "Dune.2021.1080p" });
        var old = await Add(new Collection { Name = "Dune.1984.720p" });
        var undated = await Add(new Collection { Name = "Dune.4K" });

        await _identifier.IdentifyAsync(recent.Id, new CatalogIdentifier.Request(438631, "movie"));

        Assert.Null((await Row(old.Id)).MovieId);
        Assert.NotNull((await Row(undated.Id)).MovieId);
    }

    [Fact]
    public async Task The_library_is_tried_before_tmdb()
    {
        var series = await Add(new Series { TmdbId = 1396, ManualTitle = "Breaking Bad" });
        var collection = await Add(new Collection { Name = "Breaking Bad S02E03" });

        Assert.True(await _identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request()));

        Assert.Equal(series.Id, (await Row(collection.Id)).Episode!.Season!.SeriesId);
        Assert.Empty(_tmdb.Searches);
    }

    [Fact]
    public async Task Reidentifying_a_collection_moves_it_and_prunes_what_it_leaves_empty()
    {
        _tmdb.Add(Movie(1, "Wrong"));
        _tmdb.Add(Movie(2, "Right"));
        var collection = await Add(new Collection { Name = "Something" });
        await _identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request(1, "movie"));

        var body = await Json(await _http.PostAsJsonAsync($"/collections/{collection.Id}/reidentify", new { tmdb_id = 2, media_type = "movie" }));

        var movies = await _catalog.ListMoviesAsync();
        Assert.Equal(2, Assert.Single(movies).TmdbId);
        Assert.Equal(movies[0].Id, body.GetProperty("movie_id").GetInt32());
    }

    // Movies and series

    [Fact]
    public async Task An_empty_library_lists_nothing()
    {
        Assert.Equal(0, (await Json(await _http.GetAsync("/movies"))).GetArrayLength());
        Assert.Equal(0, (await Json(await _http.GetAsync("/series"))).GetArrayLength());
    }

    [Fact]
    public async Task Movies_list_with_their_collections_and_files()
    {
        _tmdb.Add(Movie(635302, "Mugen Train", "2020-10-16"), searchAs: "Mugen Train");
        await Upload("Mugen Train (2020) 1080p.mkv", messageId: 4242);

        var movie = (await Json(await _http.GetAsync("/movies")))[0];

        Assert.Equal(635302, movie.GetProperty("tmdb_id").GetInt32());
        var file = movie.GetProperty("collections")[0].GetProperty("files")[0];
        Assert.Equal((4242, "Mugen Train (2020) 1080p.mkv", 1048576L),
            (file.GetProperty("message_id").GetInt32(), file.GetProperty("filename").GetString(), file.GetProperty("filesize").GetInt64()));
        Assert.Equal(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc), file.GetProperty("created_at").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Lookups_by_tmdb_id_return_null_when_missing()
    {
        Assert.Equal(JsonValueKind.Null, (await Json(await _http.GetAsync("/movies/tmdb/1"))).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Json(await _http.GetAsync("/series/tmdb/1"))).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Json(await _http.GetAsync("/collections/1"))).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Json(await _http.GetAsync("/files/1"))).ValueKind);
    }

    [Fact]
    public async Task Series_list_nests_seasons_episodes_and_collections()
    {
        var series = await Add(new Series { TmdbId = 1429, ManualTitle = "Attack on Titan" });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = 1 });
        var episode = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 1, Title = "To You" });
        await Add(new Collection { Name = "AoT S01E01", EpisodeId = episode.Id });
        await Add(new Collection { Name = "AoT S01", SeasonId = season.Id });

        var body = await Json(await _http.GetAsync($"/series/{series.Id}"));

        var seasonJson = body.GetProperty("seasons")[0];
        Assert.Equal("AoT S01", seasonJson.GetProperty("collections")[0].GetProperty("name").GetString());
        Assert.Equal("AoT S01E01", seasonJson.GetProperty("episodes")[0].GetProperty("collections")[0].GetProperty("name").GetString());
        Assert.False(seasonJson.GetProperty("local_metadata").GetBoolean());
    }

    [Fact]
    public async Task Missing_movies_and_series_are_404s()
    {
        Assert.Equal("Movie not found", await Detail(await _http.GetAsync("/movies/9"), HttpStatusCode.NotFound));
        Assert.Equal("Movie not found", await Detail(await _http.GetAsync("/movies/9/collections"), HttpStatusCode.NotFound));
        Assert.Equal("Series not found", await Detail(await _http.GetAsync("/series/9"), HttpStatusCode.NotFound));
        Assert.Equal("Series not found", await Detail(await _http.DeleteAsync("/series/9"), HttpStatusCode.NotFound));
        Assert.Equal("Movie not found", await Detail(await Patch("/movies/9", new { title = "x" }), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Patching_a_movie_changes_only_the_fields_sent()
    {
        var movie = await Add(new Movie { TmdbId = 1, Title = "Old", Overview = "Keep", ReleaseYear = 2000 });

        var body = await Json(await Patch($"/movies/{movie.Id}", new { title = "New", release_year = (int?)null }));

        Assert.Equal(("New", "Keep"), (body.GetProperty("title").GetString(), body.GetProperty("overview").GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("release_year").ValueKind);
        Assert.False(body.GetProperty("manually_added").GetBoolean());
    }

    [Fact]
    public async Task A_field_of_the_wrong_type_is_a_422()
    {
        var movie = await Add(new Movie { TmdbId = 1, Title = "Old" });

        Assert.Equal("release_year must be an integer",
            await Detail(await Patch($"/movies/{movie.Id}", new { release_year = "soon" }), (HttpStatusCode)422));
    }

    [Fact]
    public async Task Seasons_and_episodes_patch_by_number()
    {
        var series = await Add(new Series { TmdbId = 1, ManualTitle = "Show" });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = 2 });
        await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 5 });

        var seasonBody = await Json(await Patch($"/series/{series.Id}/seasons/2", new { local_metadata = true }));
        var episodeBody = await Json(await Patch($"/series/{series.Id}/seasons/2/episodes/5", new { title = "Five" }));

        Assert.True(seasonBody.GetProperty("local_metadata").GetBoolean());
        Assert.Equal("Five", episodeBody.GetProperty("title").GetString());
        Assert.Equal("Season not found", await Detail(await Patch($"/series/{series.Id}/seasons/3", new { local_metadata = true }), HttpStatusCode.NotFound));
        Assert.Equal("Episode not found", await Detail(await Patch($"/series/{series.Id}/seasons/2/episodes/6", new { title = "x" }), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Deleting_a_movie_leaves_its_collections_as_orphans()
    {
        var movie = await Add(new Movie { TmdbId = 1, Title = "Gone" });
        var collection = await Add(new Collection { Name = "Gone", MovieId = movie.Id });

        var body = await Json(await _http.DeleteAsync($"/movies/{movie.Id}"));

        Assert.Equal($"Movie {movie.Id} deleted", body.GetProperty("message").GetString());
        Assert.Null((await Row(collection.Id)).MovieId);
    }

    [Fact]
    public async Task Deleting_a_series_removes_its_tree_and_orphans_its_collections()
    {
        var series = await Add(new Series { TmdbId = 1, ManualTitle = "Gone" });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = 1 });
        var episode = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 1 });
        var episodeCollection = await Add(new Collection { Name = "Gone S01E01", EpisodeId = episode.Id });
        var seasonCollection = await Add(new Collection { Name = "Gone S01", SeasonId = season.Id });

        await Json(await _http.DeleteAsync($"/series/{series.Id}"));

        Assert.Equal((0, 0, 0), (await Count<Series>(), await Count<Season>(), await Count<Episode>()));
        Assert.Null((await Row(episodeCollection.Id)).EpisodeId);
        Assert.Null((await Row(seasonCollection.Id)).SeasonId);
    }

    [Fact]
    public async Task Reidentifying_a_movie_updates_it_in_place()
    {
        _tmdb.Add(Movie(2, "Right", "2010-01-01"));
        var movie = await Add(new Movie { TmdbId = 1, Title = "Wrong" });
        await Add(new Collection { Name = "Right", MovieId = movie.Id });

        var body = await Json(await _http.PostAsync($"/movies/{movie.Id}/reidentify?new_tmdb_id=2", null));

        Assert.Equal("Movie re-identified to Right", body.GetProperty("message").GetString());
        var updated = (await _catalog.GetMovieAsync(movie.Id))!;
        Assert.Equal((2, "Right", 2010, "/m2.jpg"), (updated.TmdbId!.Value, updated.Title, updated.ReleaseYear!.Value, updated.PosterPath));
    }

    [Fact]
    public async Task Reidentifying_a_movie_into_one_already_in_the_library_merges_them()
    {
        _tmdb.Add(Movie(2, "Right"));
        var wrong = await Add(new Movie { TmdbId = 1, Title = "Wrong" });
        var right = await Add(new Movie { TmdbId = 2, Title = "Right" });
        var moved = await Add(new Collection { Name = "Copy", MovieId = wrong.Id });
        await Add(new Collection { Name = "Original", MovieId = right.Id });

        await Json(await _http.PostAsync($"/movies/{wrong.Id}/reidentify?new_tmdb_id=2", null));

        Assert.Equal(right.Id, Assert.Single(await _catalog.ListMoviesAsync()).Id);
        Assert.Equal(right.Id, (await Row(moved.Id)).MovieId);
    }

    [Fact]
    public async Task Reidentifying_a_movie_to_its_own_tmdb_id_keeps_it()
    {
        _tmdb.Add(Movie(1, "Same"));
        var movie = await Add(new Movie { TmdbId = 1, Title = "Same" });
        var collection = await Add(new Collection { Name = "Same", MovieId = movie.Id });

        await Json(await _http.PostAsync($"/movies/{movie.Id}/reidentify?new_tmdb_id=1", null));

        Assert.Equal(movie.Id, (await Row(collection.Id)).MovieId);
    }

    [Fact]
    public async Task Reidentifying_a_movie_needs_a_movie_on_tmdb()
    {
        _tmdb.Add(Show(5, "A show"));
        var movie = await Add(new Movie { TmdbId = 1, Title = "Wrong" });

        Assert.Equal("New TMDB ID not found on TMDB",
            await Detail(await _http.PostAsync($"/movies/{movie.Id}/reidentify?new_tmdb_id=5", null), HttpStatusCode.NotFound));
        Assert.Equal((HttpStatusCode)422, (await _http.PostAsync($"/movies/{movie.Id}/reidentify", null)).StatusCode);
    }

    [Fact]
    public async Task Reidentifying_a_series_into_one_already_in_the_library_keeps_seasons_and_episodes()
    {
        _tmdb.Add(Show(2, "Right"));
        var wrong = await Add(new Series { TmdbId = 1, ManualTitle = "Wrong" });
        var wrongSeason = await Add(new Season { SeriesId = wrong.Id, SeasonNumber = 2 });
        var wrongEpisode = await Add(new Episode { SeasonId = wrongSeason.Id, EpisodeNumber = 7 });
        var episodeCollection = await Add(new Collection { Name = "Right S02E07", EpisodeId = wrongEpisode.Id });
        var seasonCollection = await Add(new Collection { Name = "Right S02", SeasonId = wrongSeason.Id });
        var right = await Add(new Series { TmdbId = 2, ManualTitle = "Right" });
        var rightSeason = await Add(new Season { SeriesId = right.Id, SeasonNumber = 1 });
        await Add(new Collection { Name = "Right S01", SeasonId = rightSeason.Id });

        var body = await Json(await _http.PostAsync($"/series/{wrong.Id}/reidentify?new_tmdb_id=2", null));

        Assert.Equal("Series re-identified to Right", body.GetProperty("message").GetString());
        Assert.Equal(right.Id, Assert.Single(await _catalog.ListSeriesAsync()).Id);
        var episode = (await Row(episodeCollection.Id)).Episode!;
        Assert.Equal((right.Id, 2, 7), (episode.Season!.SeriesId, episode.Season.SeasonNumber, episode.EpisodeNumber));
        Assert.Equal((right.Id, 2), ((await Row(seasonCollection.Id)).Season!.SeriesId, (await Row(seasonCollection.Id)).Season!.SeasonNumber));
    }

    [Fact]
    public async Task Reidentifying_a_series_in_place_keeps_its_episodes()
    {
        _tmdb.Add(Show(2, "Right", "2008-01-20"));
        _tmdb.TvdbIds[2] = 81189;
        var series = await Add(new Series { TmdbId = 1, ManualTitle = "Wrong" });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = 1, LocalMetadata = true });
        var episode = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 1, Title = "Pilot" });
        var collection = await Add(new Collection { Name = "Right S01E01", EpisodeId = episode.Id });

        await Json(await _http.PostAsync($"/series/{series.Id}/reidentify?new_tmdb_id=2", null));

        var updated = (await _catalog.GetSeriesAsync(series.Id))!;
        Assert.Equal((2, 81189, "Right", 2008), (updated.TmdbId!.Value, updated.TvdbId!.Value, updated.ManualTitle, updated.ReleaseYear!.Value));
        Assert.Equal(episode.Id, (await Row(collection.Id)).EpisodeId);
        Assert.Equal("Pilot", updated.Seasons.Single().Episodes.Single().Title);
    }

    [Fact]
    public async Task Posters_come_from_tmdb_and_are_empty_without_an_id()
    {
        _tmdb.Posters[("movie", 1)] = ["/a.jpg", "/b.jpg"];
        var movie = await Add(new Movie { TmdbId = 1, Title = "Posters" });
        var untracked = await Add(new Movie { Title = "No TMDB" });

        Assert.Equal(["/a.jpg", "/b.jpg"], (await _http.GetFromJsonAsync<string[]>($"/movies/{movie.Id}/posters"))!);
        Assert.Empty((await _http.GetFromJsonAsync<string[]>($"/movies/{untracked.Id}/posters"))!);
        Assert.Empty((await _http.GetFromJsonAsync<string[]>("/series/9/posters"))!);
    }

    [Fact]
    public async Task A_title_added_by_hand_survives_pruning()
    {
        _tmdb.Add(Show(1396, "Breaking Bad"));
        _tmdb.Add(Movie(1, "Temporary"));

        var body = await Json(await _http.PostAsJsonAsync("/maintenance/create-media", new { tmdb_id = 1396, media_type = "tv" }));
        var collection = await Add(new Collection { Name = "x" });
        await _identifier.IdentifyAsync(collection.Id, new CatalogIdentifier.Request(1, "movie"));
        await Json(await _http.DeleteAsync($"/collections/{collection.Id}"));

        Assert.Equal(("success", "series"), (body.GetProperty("status").GetString(), body.GetProperty("type").GetString()));
        Assert.Equal(1396, Assert.Single(await _catalog.ListSeriesAsync()).TmdbId);
        Assert.Empty(await _catalog.ListMoviesAsync());
    }

    [Fact]
    public async Task Adding_by_hand_checks_the_type_and_the_id()
    {
        Assert.Equal("Invalid media type",
            await Detail(await _http.PostAsJsonAsync("/maintenance/create-media", new { tmdb_id = 1, media_type = "book" }), HttpStatusCode.BadRequest));
        Assert.Equal("TMDB item not found",
            await Detail(await _http.PostAsJsonAsync("/maintenance/create-media", new { tmdb_id = 1, media_type = "movie" }), HttpStatusCode.NotFound));
    }

    // Maintenance

    [Fact]
    public async Task The_backup_is_a_readable_copy_of_the_database()
    {
        await Upload("zzqx-orphan.mkv");
        var before = Directory.GetFiles(Path.GetTempPath(), "cinegram-backup-*.db").Length;

        var response = await _http.GetAsync("/maintenance/backup");
        Assert.True(response.IsSuccessStatusCode);
        Assert.StartsWith("cinegram-backup-", response.Content.Headers.ContentDisposition!.FileName);
        var copy = Path.Combine(_dir, "copy.db");
        await File.WriteAllBytesAsync(copy, await response.Content.ReadAsByteArrayAsync());

        await using var db = new CinegramDb(CinegramDb.Options(copy));
        Assert.Equal("zzqx-orphan", (await db.Collections.SingleAsync()).Name);
        Assert.Equal(before, Directory.GetFiles(Path.GetTempPath(), "cinegram-backup-*.db").Length);
    }

    [Fact]
    public async Task Orphans_list_their_files()
    {
        var orphan = await Upload("zzqx-orphan.mkv");

        var body = Assert.Single((await Json(await _http.GetAsync("/maintenance/orphans"))).EnumerateArray());

        Assert.Equal((orphan.CollectionId, "zzqx-orphan", 1), (body.GetProperty("id").GetInt32(),
            body.GetProperty("name").GetString(), body.GetProperty("files_count").GetInt32()));
        Assert.Equal("zzqx-orphan.mkv", body.GetProperty("files")[0].GetProperty("filename").GetString());
    }

    [Fact]
    public async Task A_batch_identification_links_every_collection_and_reports_the_missing_ones()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));
        var first = await Upload("zzqx-a.mkv");
        var second = await Upload("zzqx-b.mkv");

        var body = await Json(await _http.PostAsJsonAsync("/maintenance/identify-batch",
            new { collection_ids = new[] { first.CollectionId, second.CollectionId, 999 }, tmdb_id = 129 }));

        Assert.Equal(["success", "success", "error"],
            body.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("status").GetString()));
        var movie = Assert.Single(await _catalog.ListMoviesAsync());
        Assert.Equal(2, movie.Collections.Count);
        Assert.Empty(await _catalog.ListOrphansAsync());
    }

    [Fact]
    public async Task A_batch_deletion_removes_the_collections_their_files_and_the_titles_left_empty()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));
        var linked = await Upload("whatever.mkv", tmdbId: 129);
        var orphan = await Upload("zzqx-orphan.mkv");
        var kept = await Upload("zzqx-kept.mkv");

        await Json(await _http.PostAsJsonAsync("/maintenance/delete-batch",
            new { collection_ids = new[] { linked.CollectionId, orphan.CollectionId, 999 } }));

        Assert.Equal(kept.CollectionId, Assert.Single(await _catalog.ListOrphansAsync()).Id);
        Assert.Equal(1, await Count<TelegramFile>());
        Assert.Empty(await _catalog.ListMoviesAsync());
    }

    [Fact]
    public async Task Batches_need_their_ids()
    {
        Assert.Equal("collection_ids and tmdb_id are required", await Detail(
            await _http.PostAsJsonAsync("/maintenance/identify-batch", new { collection_ids = new[] { 1 } }),
            HttpStatusCode.UnprocessableEntity));
        Assert.Equal("collection_ids is required", await Detail(
            await _http.PostAsJsonAsync("/maintenance/delete-batch", new { }), HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Forward_sources_group_files_by_origin_largest_first()
    {
        await Upload("zzqx-1.mkv", documentId: 1, fwdFromType: "channel", fwdFromId: "-100", fwdFromName: "Anime HD");
        await Upload("zzqx-2.mkv", documentId: 2, fwdFromType: "channel", fwdFromId: "-100", fwdFromName: "Anime HD");
        await Upload("zzqx-3.mkv", documentId: 3);
        await Upload("zzqx-4.mkv");

        var body = await Json(await _http.GetAsync("/maintenance/forward-sources"));

        var sources = body.GetProperty("sources").EnumerateArray()
            .Select(s => (s.GetProperty("type").GetString(), s.GetProperty("name").GetString(),
                s.GetProperty("files").GetInt32(), s.GetProperty("public").GetBoolean()))
            .ToList();
        Assert.Equal(("channel", "Anime HD", 2, true), sources[0]);
        Assert.Equal([("owned", "Uploaded directly", 1, false), ("unknown", "Unknown origin", 1, false)],
            sources.Skip(1).OrderBy(s => s.Item1));
        Assert.Equal((4, 4 * 1048576L),
            (body.GetProperty("total_files").GetInt32(), body.GetProperty("total_size").GetInt64()));
    }

    // Collections

    [Fact]
    public async Task Deleting_a_collection_deletes_its_files_downloads_and_the_movie_it_leaves_empty()
    {
        _tmdb.Add(Movie(1, "Lonely"));
        var result = await Upload("Lonely.mkv", tmdbId: 1);
        await Add(new DownloadTask { CollectionId = result.CollectionId });

        var body = await Json(await _http.DeleteAsync($"/collections/{result.CollectionId}"));

        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal((0, 0, 0, 0), (await Count<Collection>(), await Count<TelegramFile>(), await Count<DownloadTask>(), await Count<Movie>()));
        Assert.Equal("Collection not found", await Detail(await _http.DeleteAsync($"/collections/{result.CollectionId}"), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Creating_a_collection_validates_languages()
    {
        var created = await Json(await _http.PostAsJsonAsync("/collections", new { name = "Extras", audio_languages = "SPA,fra" }));

        Assert.Equal(("Extras", "spa,fre"), (created.GetProperty("name").GetString(), created.GetProperty("audio_languages").GetString()));
        Assert.Equal(0, created.GetProperty("files").GetArrayLength());
        Assert.Equal("Unknown language code(s): not-a-language",
            await Detail(await _http.PostAsJsonAsync("/collections", new { name = "x", audio_languages = "not-a-language" }), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Patching_technical_metadata_refreshes_quality_and_codec_unless_given()
    {
        var probed = await Add(new Collection { Name = "a" });
        var explicitValues = await Add(new Collection { Name = "b" });

        var refreshed = await Json(await Patch($"/collections/{probed.Id}",
            new { technical_metadata = CollectionMetadataTests.Probe(3840, 2160, "hevc", "smpte2084") }));
        var kept = await Json(await Patch($"/collections/{explicitValues.Id}",
            new { technical_metadata = CollectionMetadataTests.Probe(1920, 1080, "hevc"), quality = "1080p Remux", codec = "Custom Remux" }));

        Assert.Equal(("4K HDR", "H.265/HEVC"), (refreshed.GetProperty("quality").GetString(), refreshed.GetProperty("codec").GetString()));
        Assert.Equal(("1080p Remux", "Custom Remux"), (kept.GetProperty("quality").GetString(), kept.GetProperty("codec").GetString()));
    }

    [Fact]
    public async Task Patching_languages_validates_them()
    {
        var collection = await Add(new Collection { Name = "a", Notes = "keep" });

        var body = await Json(await Patch($"/collections/{collection.Id}", new { audio_languages = "spa,eng", subtitle_languages = "eng" }));

        Assert.Equal(("spa,eng", "eng", "keep"),
            (body.GetProperty("audio_languages").GetString(), body.GetProperty("subtitle_languages").GetString(), body.GetProperty("notes").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch($"/collections/{collection.Id}", new { audio_languages = "spa,not-a-language" })).StatusCode);
        Assert.Equal("spa,eng", (await Row(collection.Id)).AudioLanguages);
    }

    [Fact]
    public async Task Clearing_a_field_needs_an_explicit_null()
    {
        var collection = await Add(new Collection { Name = "a", LocalPath = "/media/a.mkv", Notes = "n" });

        await _catalog.UpdateCollectionAsync(collection.Id, new Catalog.CollectionChanges { LocalPath = null });

        var row = await Row(collection.Id);
        Assert.Equal((null, "n"), (row.LocalPath, row.Notes));
    }

    [Fact]
    public async Task An_episode_collection_moves_to_another_season_keeping_its_number()
    {
        var series = await Add(new Series { TmdbId = 1, ManualTitle = "Show" });
        var season = await Add(new Season { SeriesId = series.Id, SeasonNumber = 1 });
        var episode = await Add(new Episode { SeasonId = season.Id, EpisodeNumber = 4 });
        var collection = await Add(new Collection { Name = "Show S01E04", EpisodeId = episode.Id });

        await Json(await Patch($"/collections/{collection.Id}", new { season_number = 2 }));
        var moved = (await Row(collection.Id)).Episode!;
        Assert.Equal((2, 4), (moved.Season!.SeasonNumber, moved.EpisodeNumber));

        await Json(await Patch($"/collections/{collection.Id}", new { season_number = 2, episode_number = 9 }));
        Assert.Equal(9, (await Row(collection.Id)).Episode!.EpisodeNumber);

        await Json(await Patch($"/collections/{collection.Id}", new { season_number = 3, clear_episode = true }));
        var pack = await Row(collection.Id);
        Assert.Equal((null, 3), (pack.EpisodeId, pack.Season!.SeasonNumber));
    }

    [Fact]
    public async Task The_vocabulary_is_served()
    {
        var body = await Json(await _http.GetAsync("/collections/vocabulary"));

        Assert.Contains("4K HDR10+", body.GetProperty("qualities").EnumerateArray().Select(q => q.GetString()));
        Assert.Equal("Español", body.GetProperty("languages").GetProperty("spa").GetString());
    }

    // Files

    [Fact]
    public async Task A_file_moves_to_an_existing_collection_only()
    {
        var source = (await Upload("Postres.zip.001", "application/zip", messageId: 9001)).CollectionId;
        var target = await Add(new Collection { Name = "Target" });
        var fileId = (await FileRow(9001)).Id;

        Assert.Equal("Collection not found", await Detail(await Patch($"/files/{fileId}", new { collection_id = 999 }), HttpStatusCode.NotFound));
        var body = await Json(await Patch($"/files/{fileId}", new { collection_id = target.Id }));

        Assert.Equal(target.Id, body.GetProperty("collection_id").GetInt32());
        Assert.Empty((await Row(source)).Files);
    }

    [Fact]
    public async Task Batch_moving_files_deletes_the_collections_they_empty()
    {
        var source = (await Upload("Postres.zip.001", "application/zip", messageId: 9101)).CollectionId;
        await Upload("Postres.zip.002", "application/zip", messageId: 9102);
        var keeps = (await Upload("Otro.zip.001", "application/zip", messageId: 9103)).CollectionId;
        await Upload("Otro.zip.002", "application/zip", messageId: 9104);
        var target = await Add(new Collection { Name = "Target" });
        var ids = new[] { (await FileRow(9101)).Id, (await FileRow(9102)).Id, (await FileRow(9103)).Id };

        var body = await Json(await _http.PostAsJsonAsync("/files/batch-move", new { file_ids = ids, collection_id = target.Id }));

        Assert.Equal(("moved", 3), (body.GetProperty("status").GetString(), body.GetProperty("count").GetInt32()));
        Assert.Equal(3, (await Row(target.Id)).Files.Count);
        Assert.Null(await _catalog.GetCollectionAsync(source));
        Assert.NotNull(await _catalog.GetCollectionAsync(keeps));
        Assert.Equal("Target collection not found",
            await Detail(await _http.PostAsJsonAsync("/files/batch-move", new { file_ids = ids, collection_id = 999 }), HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Batch_deleting_files_deletes_the_collections_they_empty()
    {
        var emptied = (await Upload("Postres.zip.001", "application/zip", messageId: 9201)).CollectionId;
        var kept = (await Upload("Otro.zip.001", "application/zip", messageId: 9202)).CollectionId;
        await Upload("Otro.zip.002", "application/zip", messageId: 9203);

        var body = await Json(await _http.PostAsJsonAsync("/files/batch-delete",
            new { file_ids = new[] { (await FileRow(9201)).Id, (await FileRow(9202)).Id, 999 } }));

        Assert.Equal(3, body.GetProperty("count").GetInt32());
        Assert.Null(await _catalog.GetCollectionAsync(emptied));
        Assert.Single((await Row(kept)).Files);
    }

    [Fact]
    public async Task Deleting_one_file_keeps_its_collection()
    {
        var collection = (await Upload("Postres.zip.001", "application/zip", messageId: 9301)).CollectionId;
        var fileId = (await FileRow(9301)).Id;

        var body = await Json(await _http.DeleteAsync($"/files/{fileId}"));

        Assert.Equal(("deleted", fileId), (body.GetProperty("status").GetString(), body.GetProperty("file_id").GetInt32()));
        Assert.NotNull(await _catalog.GetCollectionAsync(collection));
        Assert.Equal("File not found", await Detail(await _http.DeleteAsync($"/files/{fileId}"), HttpStatusCode.NotFound));
    }

    // TMDB search

    [Fact]
    public async Task Search_shapes_results_for_the_web()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"), searchAs: "chihiro");
        _tmdb.Add(Show(1, "Chihiro the show"), searchAs: "chihiro");

        var results = await Json(await _http.GetAsync("/tmdb/search?query=chihiro"));

        var movie = results[0];
        Assert.Equal((129, "El viaje de Chihiro", "movie", "2001", "/m129.jpg"),
            (movie.GetProperty("id").GetInt32(), movie.GetProperty("title").GetString(), movie.GetProperty("media_type").GetString(),
                movie.GetProperty("year").GetString(), movie.GetProperty("poster_path").GetString()));
        Assert.Equal("Unknown", results[1].GetProperty("year").GetString());
        Assert.Equal(1, (await Json(await _http.GetAsync("/tmdb/search?query=chihiro&media_type=tv"))).GetArrayLength());
    }

    [Fact]
    public async Task A_numeric_search_looks_up_the_id_in_the_kinds_asked()
    {
        _tmdb.Add(Movie(129, "Movie 129"));
        _tmdb.Add(Show(129, "Show 129"));

        Assert.Equal(2, (await Json(await _http.GetAsync("/tmdb/search?query=129"))).GetArrayLength());
        var tv = await Json(await _http.GetAsync("/tmdb/search?query=129&media_type=tv"));
        Assert.Equal("Show 129", Assert.Single(tv.EnumerateArray()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Search_validates_its_parameters()
    {
        Assert.Equal((HttpStatusCode)422, (await _http.GetAsync("/tmdb/search")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await _http.GetAsync("/tmdb/search?query=x&media_type=person")).StatusCode);
    }

    [Fact]
    public async Task Search_reports_tmdb_being_down()
    {
        _tmdb.FailSearches = true;

        Assert.Equal("TMDB search failed", await Detail(await _http.GetAsync("/tmdb/search?query=x"), HttpStatusCode.BadGateway));
    }

    [Fact]
    public async Task An_uploaded_file_is_found_by_name_and_size_under_its_own_title_only()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));
        _tmdb.Add(Show(37854, "One Piece", "1999-10-20"), searchAs: "One Piece");
        await Upload("Chihiro.mkv", tmdbId: 129);
        await Upload("One Piece 1x125.mkv", tmdbId: 37854);
        await Upload("Chihiro 4K.zip.001", "application/zip", tmdbId: 129);

        Assert.True(await _catalog.HasUploadedFileAsync("movie", 129, "Chihiro.mkv", 1048576));
        Assert.False(await _catalog.HasUploadedFileAsync("movie", 129, "Chihiro.mkv", 999));
        Assert.False(await _catalog.HasUploadedFileAsync("movie", 130, "Chihiro.mkv", 1048576));
        Assert.False(await _catalog.HasUploadedFileAsync("series", 129, "Chihiro.mkv", 1048576));
        Assert.True(await _catalog.HasUploadedFileAsync("series", 37854, "One Piece 1x125.mkv", 1048576));
        Assert.True(await _catalog.HasUploadedFileAsync("movie", 129, "Chihiro 4K.zip.001", null));
    }

    [Fact]
    public async Task Undoing_an_unfinished_upload_removes_its_parts_and_keeps_finished_files()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));
        var finished = await Upload("Chihiro.mkv", tmdbId: 129);
        var partial = new PartialUpload();
        foreach (var name in new[] { "Chihiro 4K.zip.001", "Chihiro 4K.zip.002" })
        {
            var messageId = _nextMessageId++;
            partial.Sent(new StoredUpload(messageId, null));
            partial.Registered((await Upload(name, "application/zip", messageId: messageId, tmdbId: 129)).FileId);
        }
        partial.Sent(new StoredUpload(_nextMessageId++, null));
        var deleted = new List<int>();

        await partial.UndoAsync(sent =>
        {
            deleted.AddRange(sent.Select(s => s.MessageId));
            return Task.CompletedTask;
        }, _catalog);

        Assert.Equal(3, deleted.Count);
        Assert.True(partial.IsEmpty);
        await using var db = await _db.CreateDbContextAsync();
        Assert.Equal(["Chihiro.mkv"], await db.Files.Select(f => f.Filename).ToListAsync());
        Assert.Equal([finished.CollectionId], await db.Collections.Select(c => c.Id).ToListAsync());
        Assert.Single(await db.Movies.ToListAsync());
    }

    [Fact]
    public async Task Undo_still_cleans_the_catalog_when_deleting_the_messages_fails()
    {
        _tmdb.Add(Movie(129, "El viaje de Chihiro", "2001-07-20"));
        var partial = new PartialUpload();
        var messageId = _nextMessageId++;
        partial.Sent(new StoredUpload(messageId, null));
        partial.Registered((await Upload("Chihiro 4K.zip.001", "application/zip", messageId: messageId, tmdbId: 129)).FileId);

        await Assert.ThrowsAsync<AggregateException>(() =>
            partial.UndoAsync(_ => throw new InvalidOperationException("MESSAGE_DELETE_FORBIDDEN"), _catalog));

        await using var db = await _db.CreateDbContextAsync();
        Assert.Empty(await db.Files.ToListAsync());
    }

    [Fact]
    public async Task A_completed_file_is_never_undone()
    {
        var partial = new PartialUpload();
        partial.Sent(new StoredUpload(1, null));
        partial.Registered(1);
        partial.Complete();
        var called = false;

        await partial.UndoAsync(_ => { called = true; return Task.CompletedTask; }, _catalog);

        Assert.False(called);
    }

    private sealed class FakeTmdb : ITmdbApi
    {
        private readonly Dictionary<(string, int), TmdbTitle> _titles = new();
        private readonly Dictionary<string, List<TmdbTitle>> _searches = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, int> TvdbIds { get; } = new();
        public Dictionary<(string, int), List<string>> Posters { get; } = new();
        public List<(string, int)> Lookups { get; } = [];
        public List<string> Searches { get; } = [];
        public bool FailSearches { get; set; }

        public void Add(TmdbTitle title, string? searchAs = null)
        {
            _titles[(title.MediaType, title.Id)] = title;
            if (searchAs is null) return;
            if (!_searches.TryGetValue(searchAs, out var list)) _searches[searchAs] = list = [];
            list.Add(title);
        }

        public Task<TmdbTitle?> GetAsync(string mediaType, int id, CancellationToken ct = default)
        {
            Lookups.Add((mediaType, id));
            return Task.FromResult(_titles.GetValueOrDefault((mediaType, id)));
        }

        public Task<IReadOnlyList<TmdbTitle>> SearchAsync(string kind, string query, int? year, CancellationToken ct = default)
        {
            Searches.Add(query);
            if (FailSearches) throw new HttpRequestException("TMDB is down");
            var results = _searches.GetValueOrDefault(query) ?? [];
            return Task.FromResult<IReadOnlyList<TmdbTitle>>(
                results.Where(t => kind == "multi" || t.MediaType == kind).ToList());
        }

        public Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default) =>
            Task.FromResult(TvdbIds.TryGetValue(tvId, out var id) ? id : (int?)null);

        public Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Posters.GetValueOrDefault((mediaType, id)) ?? []);
    }
}
