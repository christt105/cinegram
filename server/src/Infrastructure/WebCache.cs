using Microsoft.AspNetCore.StaticFiles;

namespace Cinegram.Infrastructure;

/// <summary>
/// Cache headers for the web's static files: the entry points and the service worker are always
/// revalidated so a new release shows up on the next load, and the hashed bundles under
/// <c>/assets</c> are cached for good.
/// </summary>
public static class WebCache
{
    private static readonly HashSet<string> EntryPoints = ["index.html", "sw.js", "registerSW.js", "manifest.webmanifest"];

    public static void Apply(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;
        if (EntryPoints.Contains(context.File.Name))
            headers.CacheControl = "no-cache";
        else if (context.Context.Request.Path.StartsWithSegments("/assets"))
            headers.CacheControl = "public, max-age=31536000, immutable";
    }
}
