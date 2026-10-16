namespace Cinegram.Infrastructure;

/// <summary>
/// The running version, baked into the image as the <c>APP_VERSION</c> environment variable at
/// build time; <c>dev</c> outside an image.
/// </summary>
public sealed record AppVersion(string Current)
{
    public static AppVersion Read(Func<string, string?> read) =>
        new(read("APP_VERSION") is { Length: > 0 } version ? version : "dev");
}
