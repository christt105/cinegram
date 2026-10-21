using System.Net.Http.Headers;

namespace Cinegram.Jellyfin;

/// <summary>
/// The <c>Authorization: MediaBrowser Token="..."</c> header Jellyfin expects. Jellyfin 12 turns off
/// the legacy <c>X-Emby-Token</c> header and <c>api_key</c> query parameter by default.
/// </summary>
public static class JellyfinAuthorization
{
    public const string Scheme = "MediaBrowser";

    public static AuthenticationHeaderValue Header(string token) => new(Scheme, $"Token=\"{token}\"");
}
