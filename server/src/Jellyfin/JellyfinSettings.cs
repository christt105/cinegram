namespace Cinegram.Jellyfin;

/// <summary>
/// Where Jellyfin's API is and the token for it. Both are optional: without them the web gets no
/// Jellyfin proxy, confirmed series are identified by the folder tag alone and the startup library
/// path check is skipped.
/// </summary>
public sealed record JellyfinSettings(string? Url, string? Token)
{
    public static JellyfinSettings Read(Func<string, string?> read) =>
        new(Infrastructure.Settings.Text(read, "JELLYFIN_URL"), Infrastructure.Settings.Text(read, "JELLYFIN_TOKEN"));

    public bool IsConfigured => Url is not null && Token is not null;
}
