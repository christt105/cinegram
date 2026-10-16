using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Cinegram.Infrastructure;

namespace Cinegram.Tmdb;

/// <summary>A movie or series as TMDB returns it, in either a detail lookup or a search.</summary>
public sealed record TmdbTitle(
    int Id,
    string MediaType,
    string? Title = null,
    string? Name = null,
    string? OriginalTitle = null,
    string? OriginalName = null,
    string? ReleaseDate = null,
    string? FirstAirDate = null,
    double Popularity = 0,
    double? MatchScore = null,
    string? PosterPath = null,
    string? Overview = null)
{
    public string? DisplayTitle => (MediaType == "movie" ? Title : Name) ?? Title ?? Name;

    public int? Year
    {
        get
        {
            var date = MediaType == "movie" ? ReleaseDate : FirstAirDate;
            return date is { Length: >= 4 } && int.TryParse(date[..4], CultureInfo.InvariantCulture, out var year)
                ? year
                : null;
        }
    }
}

/// <summary>The TMDB endpoints identification needs.</summary>
public interface ITmdbApi
{
    /// <summary>Null when the id doesn't exist or the lookup fails.</summary>
    Task<TmdbTitle?> GetAsync(string mediaType, int id, CancellationToken ct = default);

    /// <summary><paramref name="kind"/> is movie, tv or multi; <paramref name="year"/> is ignored
    /// for multi. Throws when TMDB can't be reached.</summary>
    Task<IReadOnlyList<TmdbTitle>> SearchAsync(string kind, string query, int? year, CancellationToken ct = default);

    Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default);

    /// <summary>File paths of every poster TMDB has for the title, in any language.</summary>
    Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int id, CancellationToken ct = default);
}

/// <summary>A search result as the web and the bot list it. <see cref="Year"/> is "Unknown"
/// without a date.</summary>
public sealed record TmdbSearchHit(int Id, string? Title, string MediaType, string Year, string? PosterPath, string? Overview)
{
    public static TmdbSearchHit From(TmdbTitle t) => new(
        t.Id,
        t.MediaType == "movie" ? t.Title : t.Name,
        t.MediaType,
        (t.MediaType == "movie" ? t.ReleaseDate : t.FirstAirDate)?.Split('-')[0] ?? "Unknown",
        t.PosterPath,
        t.Overview);
}

/// <summary>Identification of a file against TMDB, and the lookups the catalogue needs.</summary>
public interface ITmdbLookup
{
    Task<TmdbTitle?> IdentifyByFilenameAsync(string filename, CancellationToken ct = default);

    /// <summary>Tries <paramref name="mediaTypeHint"/> first, since movies and series don't share
    /// an id space, then the other kind.</summary>
    Task<TmdbTitle?> IdentifyByTmdbIdAsync(int tmdbId, string? mediaTypeHint, CancellationToken ct = default);

    Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default);

    /// <summary>Exactly <paramref name="mediaType"/>, without trying the other kind. Null when the
    /// id doesn't exist or the lookup fails.</summary>
    Task<TmdbTitle?> GetAsync(string mediaType, int tmdbId, CancellationToken ct = default);

    /// <summary>By title, or by id when <paramref name="query"/> is a number. <paramref name="kind"/>
    /// is movie, tv or multi. Throws when TMDB can't be reached.</summary>
    Task<IReadOnlyList<TmdbSearchHit>> SearchAsync(string query, string kind, CancellationToken ct = default);

    /// <summary>Empty when the lookup fails.</summary>
    Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int tmdbId, CancellationToken ct = default);
}

public class TmdbIdentifier(ITmdbApi api) : ITmdbLookup
{
    public async Task<TmdbTitle?> IdentifyByTmdbIdAsync(int tmdbId, string? mediaTypeHint, CancellationToken ct = default)
    {
        if (tmdbId <= 0) throw new ArgumentOutOfRangeException(nameof(tmdbId), "No TMDB id provided.");

        if (mediaTypeHint == "tv")
            return await api.GetAsync("tv", tmdbId, ct) ?? await api.GetAsync("movie", tmdbId, ct);

        return await api.GetAsync("movie", tmdbId, ct) ?? await api.GetAsync("tv", tmdbId, ct);
    }

    public Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default) => api.GetTvdbIdAsync(tvId, ct);

    public Task<TmdbTitle?> GetAsync(string mediaType, int tmdbId, CancellationToken ct = default) =>
        api.GetAsync(mediaType, tmdbId, ct);

    public Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int tmdbId, CancellationToken ct = default) =>
        api.GetPosterPathsAsync(mediaType, tmdbId, ct);

    public async Task<IReadOnlyList<TmdbSearchHit>> SearchAsync(string query, string kind, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length > 0 && query.All(char.IsAsciiDigit) && int.TryParse(query, out var id))
        {
            var byId = new List<TmdbTitle?>();
            if (kind is "movie" or "multi") byId.Add(await api.GetAsync("movie", id, ct));
            if (kind is "tv" or "multi") byId.Add(await api.GetAsync("tv", id, ct));
            return byId.OfType<TmdbTitle>().Select(TmdbSearchHit.From).ToList();
        }

        return (await api.SearchAsync(kind, query, null, ct)).Select(TmdbSearchHit.From).ToList();
    }

    public async Task<TmdbTitle?> IdentifyByFilenameAsync(string filename, CancellationToken ct = default)
    {
        var parsed = ReleaseNameParser.Parse(filename);
        if (parsed.TmdbId is { } tagged and > 0)
        {
            var byId = await IdentifyByTmdbIdAsync(tagged, parsed.Type, ct);
            if (byId is not null) return byId;
        }

        var match = await SearchAndMatchAsync(parsed.CleanName, parsed.Type, parsed.Year, ct);
        if (match is not null) return match;

        var normalized = TitleMatcher.Normalize(parsed.CleanName);
        if (normalized.Length > 0 && normalized != parsed.CleanName.ToLowerInvariant())
            return await SearchAndMatchAsync(normalized, parsed.Type, parsed.Year, ct);

        return null;
    }

    /// <summary>The year goes to TMDB's own year filter rather than into the query: search is a
    /// literal text match, and a year missing from the title returns nothing at all.</summary>
    public async Task<TmdbTitle?> SearchAndMatchAsync(string query, string type, int? year, CancellationToken ct = default)
    {
        TmdbTitle? match = null;
        if (type is "movie" or "tv")
            match = TitleMatcher.BestMatch(await api.SearchAsync(type, query, year, ct), query);

        return match ?? TitleMatcher.BestMatch(await api.SearchAsync("multi", query, null, ct), query);
    }
}

public class TmdbApi : ITmdbApi, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _language;
    private readonly ConcurrentDictionary<(string, int), TmdbTitle> _details = new();

    public TmdbApi(string apiKey, string language, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        _http.Timeout = TimeSpan.FromSeconds(15);
        _apiKey = apiKey;
        _language = language;
    }

    public async Task<TmdbTitle?> GetAsync(string mediaType, int id, CancellationToken ct = default)
    {
        if (_details.TryGetValue((mediaType, id), out var cached)) return cached;

        try
        {
            using var doc = await GetJsonAsync($"{mediaType}/{id}", [("language", _language)], ct);
            var title = Parse(doc.RootElement, mediaType);
            if (title is not null) _details[(mediaType, id)] = title;
            return title;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log.Error($"[TMDB] Invalid TMDB id for {mediaType}: {id}", ex);
            return null;
        }
    }

    public async Task<IReadOnlyList<TmdbTitle>> SearchAsync(string kind, string query, int? year, CancellationToken ct = default)
    {
        var parameters = new List<(string, string)> { ("query", query), ("language", _language) };
        if (year is { } y && kind == "movie") parameters.Add(("year", y.ToString(CultureInfo.InvariantCulture)));
        if (year is { } t && kind == "tv") parameters.Add(("first_air_date_year", t.ToString(CultureInfo.InvariantCulture)));

        using var doc = await GetJsonAsync($"search/{kind}", parameters, ct);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return [];

        return results.EnumerateArray()
            .Select(r => Parse(r, kind == "multi" ? null : kind))
            .OfType<TmdbTitle>()
            .ToList();
    }

    public async Task<int?> GetTvdbIdAsync(int tvId, CancellationToken ct = default)
    {
        try
        {
            using var doc = await GetJsonAsync($"tv/{tvId}/external_ids", [], ct);
            return doc.RootElement.TryGetProperty("tvdb_id", out var tvdb) && tvdb.ValueKind == JsonValueKind.Number
                ? tvdb.GetInt32()
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log.Error($"[TMDB] Error fetching TVDB id for tmdb_id={tvId}", ex);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetPosterPathsAsync(string mediaType, int id, CancellationToken ct = default)
    {
        try
        {
            using var doc = await GetJsonAsync($"{mediaType}/{id}/images", [], ct);
            if (!doc.RootElement.TryGetProperty("posters", out var posters) || posters.ValueKind != JsonValueKind.Array)
                return [];

            return posters.EnumerateArray().Select(p => Text(p, "file_path")).OfType<string>().ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log.Error($"[TMDB] Error fetching posters for {mediaType} {id}", ex);
            return [];
        }
    }

    /// <summary>Null for results that are neither a movie nor a series (people, in a multi search).</summary>
    public static TmdbTitle? Parse(JsonElement e, string? mediaType)
    {
        mediaType ??= Text(e, "media_type");
        if (mediaType is not ("movie" or "tv") || !e.TryGetProperty("id", out var id)) return null;

        return new TmdbTitle(
            id.GetInt32(),
            mediaType,
            Text(e, "title"),
            Text(e, "name"),
            Text(e, "original_title"),
            Text(e, "original_name"),
            Text(e, "release_date"),
            Text(e, "first_air_date"),
            e.TryGetProperty("popularity", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0,
            PosterPath: Text(e, "poster_path"),
            Overview: e.TryGetProperty("overview", out var overview) && overview.ValueKind == JsonValueKind.String
                ? overview.GetString()
                : null);
    }

    private static string? Text(JsonElement e, string property) =>
        e.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private async Task<JsonDocument> GetJsonAsync(
        string path, IEnumerable<(string Key, string Value)> parameters, CancellationToken ct)
    {
        var query = string.Join("&",
            parameters.Prepend(("api_key", _apiKey)).Select(p => $"{p.Item1}={WebUtility.UrlEncode(p.Item2)}"));
        using var response = await _http.GetAsync($"{path}?{query}", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public void Dispose() => _http.Dispose();
}
