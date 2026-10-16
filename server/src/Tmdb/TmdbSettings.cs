namespace Cinegram.Tmdb;

/// <summary>The TMDB API key and the language titles and overviews are fetched in.</summary>
public sealed record TmdbSettings(string ApiKey, string Language)
{
    public const string DefaultLanguage = "en-US";

    public static TmdbSettings Read(Func<string, string?> read) => new(
        read("TMDB_API_KEY") ?? "",
        read("TMDB_CONTENT_LANGUAGE") is { Length: > 0 } language ? language : DefaultLanguage);
}
