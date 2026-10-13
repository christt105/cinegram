using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bot.Data;
using Bot.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Bot.Services;

/// <summary>The library API the web uses: movies, series, collections, files, TMDB search and
/// adding a title by hand.</summary>
public static class CatalogEndpoints
{
    public sealed record FileOut(int Id, int MessageId, string Filename, long Filesize, string? MimeType, DateTime CreatedAt,
        int CollectionId);

    public sealed record CollectionOut(
        int Id, string? Name, int? MovieId, int? EpisodeId, int? SeasonId, List<FileOut> Files, string? Quality, string? Codec,
        string? AudioLanguages, string? SubtitleLanguages, string? Tags, string? Notes, string? TechnicalMetadata,
        string? LocalPath);

    public sealed record EpisodeOut(int Id, int EpisodeNumber, string? Title, List<CollectionOut> Collections);

    public sealed record SeasonOut(int Id, int SeasonNumber, bool LocalMetadata, List<EpisodeOut> Episodes,
        List<CollectionOut> Collections);

    public sealed record SeriesOut(
        int Id, int? TmdbId, int? TvdbId, string? ManualTitle, string? PosterPath, string? Overview, int? ReleaseYear,
        List<SeasonOut> Seasons, DateTime CreatedAt);

    public sealed record MovieOut(
        int Id, int? TmdbId, string? Title, string? PosterPath, List<CollectionOut> Collections, int? ReleaseYear,
        string? Overview, string? Tags, string? Notes, DateTime CreatedAt);

    public record IdentifyRequest(
        [property: JsonPropertyName("tmdb_id")] int? TmdbId,
        [property: JsonPropertyName("media_type")] string? MediaType,
        [property: JsonPropertyName("season_number")] int? SeasonNumber,
        [property: JsonPropertyName("episode_number")] int? EpisodeNumber);

    public record CreateCollectionRequest(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("movie_id")] int? MovieId,
        [property: JsonPropertyName("season_id")] int? SeasonId,
        [property: JsonPropertyName("episode_id")] int? EpisodeId,
        [property: JsonPropertyName("quality")] string? Quality,
        [property: JsonPropertyName("codec")] string? Codec,
        [property: JsonPropertyName("audio_languages")] string? AudioLanguages,
        [property: JsonPropertyName("subtitle_languages")] string? SubtitleLanguages,
        [property: JsonPropertyName("tags")] string? Tags,
        [property: JsonPropertyName("notes")] string? Notes);

    public record FileIdsRequest(
        [property: JsonPropertyName("file_ids")] List<int>? FileIds,
        [property: JsonPropertyName("collection_id")] int? CollectionId);

    public record CreateMediaRequest(
        [property: JsonPropertyName("tmdb_id")] int? TmdbId,
        [property: JsonPropertyName("media_type")] string? MediaType);

    private static readonly string[] SearchKinds = ["multi", "movie", "tv"];

    public static void Map(IEndpointRouteBuilder app)
    {
        MapMovies(app);
        MapSeries(app);
        MapCollections(app);
        MapFiles(app);

        app.MapGet("/tmdb/search", async (string? query, [FromQuery(Name = "media_type")] string? mediaType,
            ITmdbLookup tmdb, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(query)) return Error(422, "query is required");
            mediaType ??= "multi";
            if (!SearchKinds.Contains(mediaType)) return Error(422, "media_type must be multi, movie or tv");

            try
            {
                return Json(await tmdb.SearchAsync(query, mediaType, ct));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Log.Error($"[Catalog] TMDB search failed for '{query}'", ex);
                return Error(502, "TMDB search failed");
            }
        });

        app.MapPost("/maintenance/create-media", async ([FromBody] CreateMediaRequest? request, CatalogIdentifier identifier,
            CancellationToken ct) =>
        {
            if (request is not { TmdbId: { } tmdbId, MediaType: { } mediaType })
                return Error(422, "tmdb_id and media_type are required");

            var outcome = await identifier.CreateManualAsync(tmdbId, mediaType, ct);
            return outcome.Error switch
            {
                CatalogIdentifier.CreateError.InvalidMediaType => Error(400, "Invalid media type"),
                CatalogIdentifier.CreateError.TmdbNotFound => Error(404, "TMDB item not found"),
                _ => Json(new { status = "success", type = outcome.Type, id = outcome.Id }),
            };
        });
    }

    private static void MapMovies(IEndpointRouteBuilder app)
    {
        app.MapGet("/movies", async (Catalog catalog, CancellationToken ct) =>
            Json((await catalog.ListMoviesAsync(ct)).Select(ToOut)));

        app.MapGet("/movies/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.GetMovieAsync(id, ct) is { } movie ? Json(ToOut(movie)) : Error(404, "Movie not found"));

        app.MapGet("/movies/tmdb/{tmdbId:int}", async (int tmdbId, Catalog catalog, CancellationToken ct) =>
            await catalog.GetMovieByTmdbAsync(tmdbId, ct) is { } movie ? Json(ToOut(movie)) : JsonNull());

        app.MapGet("/movies/{id:int}/collections", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.GetMovieAsync(id, ct) is { } movie
                ? Json(movie.Collections.Select(ToOut))
                : Error(404, "Movie not found"));

        app.MapGet("/movies/{id:int}/posters", async (int id, Catalog catalog, ITmdbLookup tmdb, CancellationToken ct) =>
            await catalog.GetMovieAsync(id, ct) is { TmdbId: { } tmdbId }
                ? Json(await tmdb.GetPosterPathsAsync("movie", tmdbId, ct))
                : Json(Array.Empty<string>()));

        app.MapDelete("/movies/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.DeleteMovieAsync(id, ct)
                ? Json(new { status = "ok", message = $"Movie {id} deleted" })
                : Error(404, "Movie not found"));

        app.MapPatch("/movies/{id:int}", async (int id, HttpRequest http, Catalog catalog, CancellationToken ct) =>
        {
            var patch = await PatchBody.ReadAsync(http, ct);
            var title = patch.String("title");
            var posterPath = patch.String("poster_path");
            var overview = patch.String("overview");
            var releaseYear = patch.Int("release_year");
            if (patch.Error is { } error) return Error(422, error);

            var movie = await catalog.UpdateMovieAsync(id, m =>
            {
                if (patch.Has("title")) m.Title = title;
                if (patch.Has("poster_path")) m.PosterPath = posterPath;
                if (patch.Has("overview")) m.Overview = overview;
                if (patch.Has("release_year")) m.ReleaseYear = releaseYear;
            }, ct);
            return movie is null ? Error(404, "Movie not found") : Json(Row(movie));
        });

        app.MapPost("/movies/{id:int}/reidentify", async (int id, [FromQuery(Name = "new_tmdb_id")] int? newTmdbId,
            CatalogIdentifier identifier, CancellationToken ct) =>
        {
            if (newTmdbId is not { } tmdbId) return Error(422, "new_tmdb_id is required");

            var outcome = await identifier.ReidentifyMovieAsync(id, tmdbId, ct);
            return outcome.Error switch
            {
                CatalogIdentifier.ReidentifyError.NotFound => Error(404, "Movie not found"),
                CatalogIdentifier.ReidentifyError.TmdbNotFound => Error(404, "New TMDB ID not found on TMDB"),
                _ => Json(new { status = "ok", message = $"Movie re-identified to {outcome.Title}" }),
            };
        });
    }

    private static void MapSeries(IEndpointRouteBuilder app)
    {
        app.MapGet("/series", async (Catalog catalog, CancellationToken ct) =>
            Json((await catalog.ListSeriesAsync(ct)).Select(ToOut)));

        app.MapGet("/series/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.GetSeriesAsync(id, ct) is { } series ? Json(ToOut(series)) : Error(404, "Series not found"));

        app.MapGet("/series/tmdb/{tmdbId:int}", async (int tmdbId, Catalog catalog, CancellationToken ct) =>
            await catalog.GetSeriesByTmdbAsync(tmdbId, ct) is { } series ? Json(ToOut(series)) : JsonNull());

        app.MapGet("/series/{id:int}/posters", async (int id, Catalog catalog, ITmdbLookup tmdb, CancellationToken ct) =>
            await catalog.GetSeriesAsync(id, ct) is { TmdbId: { } tmdbId }
                ? Json(await tmdb.GetPosterPathsAsync("tv", tmdbId, ct))
                : Json(Array.Empty<string>()));

        app.MapDelete("/series/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.DeleteSeriesAsync(id, ct)
                ? Json(new { status = "ok", message = $"Series {id} deleted" })
                : Error(404, "Series not found"));

        app.MapPatch("/series/{id:int}", async (int id, HttpRequest http, Catalog catalog, CancellationToken ct) =>
        {
            var patch = await PatchBody.ReadAsync(http, ct);
            var title = patch.String("manual_title");
            var posterPath = patch.String("poster_path");
            var overview = patch.String("overview");
            var releaseYear = patch.Int("release_year");
            if (patch.Error is { } error) return Error(422, error);

            var series = await catalog.UpdateSeriesAsync(id, s =>
            {
                if (patch.Has("manual_title")) s.ManualTitle = title;
                if (patch.Has("poster_path")) s.PosterPath = posterPath;
                if (patch.Has("overview")) s.Overview = overview;
                if (patch.Has("release_year")) s.ReleaseYear = releaseYear;
            }, ct);
            return series is null ? Error(404, "Series not found") : Json(Row(series));
        });

        app.MapPatch("/series/{id:int}/seasons/{seasonNumber:int}",
            async (int id, int seasonNumber, HttpRequest http, Catalog catalog, CancellationToken ct) =>
            {
                var patch = await PatchBody.ReadAsync(http, ct);
                var localMetadata = patch.Bool("local_metadata", nullable: false);
                if (patch.Error is { } error) return Error(422, error);

                var season = await catalog.UpdateSeasonAsync(id, seasonNumber, s =>
                {
                    if (localMetadata is { } value) s.LocalMetadata = value;
                }, ct);
                return season is null ? Error(404, "Season not found") : Json(Row(season));
            });

        app.MapPatch("/series/{id:int}/seasons/{seasonNumber:int}/episodes/{episodeNumber:int}",
            async (int id, int seasonNumber, int episodeNumber, HttpRequest http, Catalog catalog, CancellationToken ct) =>
            {
                var patch = await PatchBody.ReadAsync(http, ct);
                var title = patch.String("title");
                if (patch.Error is { } error) return Error(422, error);

                var (episode, missing) = await catalog.UpdateEpisodeAsync(id, seasonNumber, episodeNumber, e =>
                {
                    if (patch.Has("title")) e.Title = title;
                }, ct);
                return missing switch
                {
                    Catalog.EpisodeUpdateError.SeasonNotFound => Error(404, "Season not found"),
                    Catalog.EpisodeUpdateError.EpisodeNotFound => Error(404, "Episode not found"),
                    _ => Json(Row(episode!)),
                };
            });

        app.MapPost("/series/{id:int}/reidentify", async (int id, [FromQuery(Name = "new_tmdb_id")] int? newTmdbId,
            CatalogIdentifier identifier, CancellationToken ct) =>
        {
            if (newTmdbId is not { } tmdbId) return Error(422, "new_tmdb_id is required");

            var outcome = await identifier.ReidentifySeriesAsync(id, tmdbId, ct);
            return outcome.Error switch
            {
                CatalogIdentifier.ReidentifyError.NotFound => Error(404, "Series not found"),
                CatalogIdentifier.ReidentifyError.TmdbNotFound => Error(404, "New TMDB ID not found on TMDB"),
                _ => Json(new { status = "ok", message = $"Series re-identified to {outcome.Title}" }),
            };
        });
    }

    private static void MapCollections(IEndpointRouteBuilder app)
    {
        app.MapGet("/collections/vocabulary", () => Json(new
        {
            qualities = CollectionMetadata.Qualities,
            codecs = CollectionMetadata.Codecs,
            languages = CollectionMetadata.Languages,
        }));

        app.MapGet("/collections/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.GetCollectionAsync(id, ct) is { } collection ? Json(ToOut(collection)) : JsonNull());

        app.MapDelete("/collections/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.DeleteCollectionAsync(id, ct)
                ? Json(new { status = "ok", message = $"Collection {id} deleted" })
                : Error(404, "Collection not found"));

        app.MapPost("/collections", async ([FromBody] CreateCollectionRequest? request, Catalog catalog, CancellationToken ct) =>
        {
            if (request is not { Name: { } name }) return Error(422, "name is required");

            try
            {
                var collection = await catalog.CreateCollectionAsync(new Catalog.CollectionDraft(
                    name, request.MovieId, request.SeasonId, request.EpisodeId, request.Quality, request.Codec,
                    request.AudioLanguages, request.SubtitleLanguages, request.Tags, request.Notes), ct);
                return Json(ToOut(collection));
            }
            catch (InvalidLanguageCodeException ex)
            {
                return Error(400, ex.Message);
            }
        });

        app.MapPatch("/collections/{id:int}", async (int id, HttpRequest http, Catalog catalog, CancellationToken ct) =>
        {
            var patch = await PatchBody.ReadAsync(http, ct);
            var changes = new Catalog.CollectionChanges();
            foreach (var field in (string[])["name", "quality", "codec", "audio_languages", "subtitle_languages", "tags",
                         "notes", "technical_metadata", "local_path"])
            {
                var value = patch.String(field);
                if (!patch.Has(field)) continue;
                switch (field)
                {
                    case "name": changes.Name = value; break;
                    case "quality": changes.Quality = value; break;
                    case "codec": changes.Codec = value; break;
                    case "audio_languages": changes.AudioLanguages = value; break;
                    case "subtitle_languages": changes.SubtitleLanguages = value; break;
                    case "tags": changes.Tags = value; break;
                    case "notes": changes.Notes = value; break;
                    case "technical_metadata": changes.TechnicalMetadata = value; break;
                    case "local_path": changes.LocalPath = value; break;
                }
            }
            changes.SeasonNumber = patch.Int("season_number");
            changes.EpisodeNumber = patch.Int("episode_number");
            changes.ClearEpisode = patch.Bool("clear_episode") ?? false;
            if (patch.Error is { } error) return Error(422, error);

            try
            {
                var collection = await catalog.UpdateCollectionAsync(id, changes, ct);
                return collection is null ? Error(404, "Collection not found") : Json(Row(collection));
            }
            catch (InvalidLanguageCodeException ex)
            {
                return Error(400, ex.Message);
            }
        });

        app.MapPost("/collections/{id:int}/identify", async (int id, [FromBody] IdentifyRequest? request, Catalog catalog,
            CatalogIdentifier identifier, CancellationToken ct) =>
        {
            if (request is not { TmdbId: { } tmdbId }) return Error(422, "tmdb_id is required");
            if (await catalog.GetCollectionAsync(id, ct) is null) return Error(404, "Collection not found");

            var linked = await identifier.IdentifyAsync(id, new CatalogIdentifier.Request(
                tmdbId, request.MediaType, request.SeasonNumber, request.EpisodeNumber), ct);
            if (!linked) return Error(404, $"TMDB ID {tmdbId} not found on TMDB");
            return Json(ToOut((await catalog.GetCollectionAsync(id, ct))!));
        });

        app.MapPost("/collections/{id:int}/reidentify", async (int id, [FromBody] IdentifyRequest? request, Catalog catalog,
            CatalogIdentifier identifier, CancellationToken ct) =>
        {
            if (await catalog.GetCollectionAsync(id, ct) is null) return Error(404, "Collection not found");

            await identifier.ReidentifyAsync(id, new CatalogIdentifier.Request(
                request?.TmdbId, request?.MediaType, request?.SeasonNumber, request?.EpisodeNumber), ct);
            return Json(ToOut((await catalog.GetCollectionAsync(id, ct))!));
        });
    }

    private static void MapFiles(IEndpointRouteBuilder app)
    {
        app.MapGet("/files/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.GetFileAsync(id, ct) is { } file ? Json(ToOut(file)) : JsonNull());

        app.MapDelete("/files/{id:int}", async (int id, Catalog catalog, CancellationToken ct) =>
            await catalog.DeleteFileAsync(id, ct)
                ? Json(new { status = "deleted", file_id = id })
                : Error(404, "File not found"));

        app.MapPatch("/files/{id:int}", async (int id, HttpRequest http, Catalog catalog, CancellationToken ct) =>
        {
            var patch = await PatchBody.ReadAsync(http, ct);
            var collectionId = patch.Int("collection_id", nullable: false);
            if (patch.Error is { } error) return Error(422, error);
            if (collectionId is not { } target)
                return await catalog.GetFileAsync(id, ct) is { } unchanged ? Json(Row(unchanged)) : Error(404, "File not found");

            var (file, missing) = await catalog.MoveFileAsync(id, target, ct);
            return missing switch
            {
                Catalog.FileMoveError.FileNotFound => Error(404, "File not found"),
                Catalog.FileMoveError.CollectionNotFound => Error(404, "Collection not found"),
                _ => Json(Row(file!)),
            };
        });

        app.MapPost("/files/batch-delete", async ([FromBody] FileIdsRequest? request, Catalog catalog, CancellationToken ct) =>
        {
            if (request is not { FileIds: { } ids }) return Error(422, "file_ids is required");

            await catalog.DeleteFilesAsync(ids, ct);
            return Json(new { status = "deleted", count = ids.Count });
        });

        app.MapPost("/files/batch-move", async ([FromBody] FileIdsRequest? request, Catalog catalog, CancellationToken ct) =>
        {
            if (request is not { FileIds: { } ids, CollectionId: { } collectionId })
                return Error(422, "file_ids and collection_id are required");

            return await catalog.MoveFilesAsync(ids, collectionId, ct)
                ? Json(new { status = "moved", count = ids.Count })
                : Error(404, "Target collection not found");
        });
    }

    public static MovieOut ToOut(Movie m) => new(
        m.Id, m.TmdbId, m.Title, m.PosterPath, m.Collections.Select(ToOut).ToList(), m.ReleaseYear, m.Overview, m.Tags,
        m.Notes, m.CreatedAt);

    public static SeriesOut ToOut(Series s) => new(
        s.Id, s.TmdbId, s.TvdbId, s.ManualTitle, s.PosterPath, s.Overview, s.ReleaseYear,
        s.Seasons.Select(se => new SeasonOut(
            se.Id, se.SeasonNumber, se.LocalMetadata,
            se.Episodes.Select(e => new EpisodeOut(e.Id, e.EpisodeNumber, e.Title, e.Collections.Select(ToOut).ToList())).ToList(),
            se.Collections.Select(ToOut).ToList())).ToList(),
        s.CreatedAt);

    public static CollectionOut ToOut(Collection c) => new(
        c.Id, c.Name, c.MovieId, c.EpisodeId, c.SeasonId, c.Files.Select(ToOut).ToList(), c.Quality, c.Codec,
        c.AudioLanguages, c.SubtitleLanguages, c.Tags, c.Notes, c.TechnicalMetadata, c.LocalPath);

    public static FileOut ToOut(TelegramFile f) =>
        new(f.Id, f.MessageId, f.Filename, f.Filesize, f.MimeType, f.CreatedAt, f.CollectionId);

    private static object Row(Movie m) => new
    {
        m.Id, m.TmdbId, m.Title, m.PosterPath, m.ReleaseYear, m.Overview, m.Tags, m.Notes, m.ManuallyAdded, m.CreatedAt,
    };

    private static object Row(Series s) => new
    {
        s.Id, s.TmdbId, s.TvdbId, s.ManualTitle, s.PosterPath, s.Overview, s.ReleaseYear, s.Tags, s.Notes, s.ManuallyAdded,
        s.CreatedAt,
    };

    private static object Row(Season s) => new { s.Id, s.SeriesId, s.SeasonNumber, s.Tags, s.Notes, s.LocalMetadata };

    private static object Row(Episode e) => new { e.Id, e.SeasonId, e.EpisodeNumber, e.Title, e.Tags, e.Notes };

    private static object Row(Collection c) => new
    {
        c.Id, c.Name, c.Quality, c.Codec, c.AudioLanguages, c.SubtitleLanguages, c.Tags, c.Notes, c.TechnicalMetadata,
        c.LocalPath, c.MovieId, c.SeasonId, c.EpisodeId,
    };

    private static object Row(TelegramFile f) => new
    {
        f.Id, f.MessageId, f.UserMessageId, f.Filename, f.Filesize, f.MimeType, f.CreatedAt, f.StoragePeer, f.DocumentId,
        f.FwdFromType, f.FwdFromId, f.FwdFromName, f.FwdFromHidden, f.CollectionId,
    };

    private static IResult Json(object value) => Results.Json(value, WatchEndpoints.Json);

    private static IResult JsonNull() => Results.Content("null", "application/json");

    private static IResult Error(int statusCode, string detail) =>
        Results.Json(new { detail }, WatchEndpoints.Json, statusCode: statusCode);

    /// <summary>
    /// A partial update: only the fields present in the body change, and an explicit null clears
    /// one. Unknown fields are ignored; a field of the wrong type sets <see cref="Error"/>.
    /// </summary>
    private sealed class PatchBody(JsonObject body)
    {
        public string? Error { get; private set; }

        public static async Task<PatchBody> ReadAsync(HttpRequest http, CancellationToken ct)
        {
            try
            {
                var node = await JsonNode.ParseAsync(http.Body, cancellationToken: ct);
                return node is JsonObject obj ? new(obj) : new(new JsonObject()) { Error = "Body must be a JSON object" };
            }
            catch (JsonException)
            {
                return new(new JsonObject()) { Error = "Invalid JSON body" };
            }
        }

        public bool Has(string field) => body.ContainsKey(field);

        public string? String(string field)
        {
            if (!Present(field, true, out var value)) return null;
            if (value!.TryGetValue<string>(out var text)) return text;
            Error ??= $"{field} must be a string";
            return null;
        }

        public int? Int(string field, bool nullable = true)
        {
            if (!Present(field, nullable, out var value)) return null;
            if (value!.TryGetValue<int>(out var number)) return number;
            Error ??= $"{field} must be an integer";
            return null;
        }

        public bool? Bool(string field, bool nullable = true)
        {
            if (!Present(field, nullable, out var value)) return null;
            if (value!.TryGetValue<bool>(out var flag)) return flag;
            Error ??= $"{field} must be a boolean";
            return null;
        }

        /// <summary>False when the field is missing, null or not a scalar.</summary>
        private bool Present(string field, bool nullable, out JsonValue? value)
        {
            value = null;
            if (!body.TryGetPropertyValue(field, out var node)) return false;
            if (node is null)
            {
                if (!nullable) Error ??= $"{field} can't be null";
                return false;
            }

            value = node as JsonValue;
            if (value is null) Error ??= $"{field} must be a scalar";
            return value is not null;
        }
    }
}
