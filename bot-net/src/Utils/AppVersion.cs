namespace Bot.Utils;

/// <summary>
/// Exposes the running bot version, baked into the image as the
/// <c>APP_VERSION</c> environment variable at build time.
/// </summary>
public static class AppVersion
{
    public static string Current =>
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } v ? v : "dev";
}
