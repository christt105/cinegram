using System.Text.RegularExpressions;

namespace Cinegram.Services;

/// <summary>
/// Read-only pass-through to Jellyfin for the web, which no longer receives the Jellyfin token:
/// <c>GET /jellyfin/{path}</c> is forwarded to <c>JELLYFIN_URL</c> with the server's token. Only
/// the item, user and show endpoints the web reads are reachable, and only with GET.
/// </summary>
public partial class JellyfinProxy : IDisposable
{
    private static readonly string[] ForwardedResponseHeaders = ["Cache-Control", "ETag", "Last-Modified", "Expires"];

    private readonly HttpClient _http;

    public JellyfinProxy(string? baseUrl, string? token, HttpMessageHandler? handler = null)
    {
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Add("X-Emby-Token", token);
        IsConfigured = BaseUrl is not null && !string.IsNullOrWhiteSpace(token);
    }

    public static JellyfinProxy FromEnvironment() => new(
        Environment.GetEnvironmentVariable("JELLYFIN_URL"),
        Environment.GetEnvironmentVariable("JELLYFIN_TOKEN"));

    public string? BaseUrl { get; }

    public bool IsConfigured { get; }

    public void Dispose() => _http.Dispose();

    [GeneratedRegex("^(Users|Items|Shows)(/[A-Za-z0-9_-]+)*$")]
    private static partial Regex AllowedPath();

    public static bool IsAllowed(string path) => AllowedPath().IsMatch(path);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/jellyfin", (JellyfinProxy proxy) =>
            Results.Ok(new { url = proxy.BaseUrl, configured = proxy.IsConfigured }));

        app.MapGet("/jellyfin/{**path}", async (string path, HttpContext context, JellyfinProxy proxy) =>
        {
            if (!proxy.IsConfigured)
                return Results.Problem("JELLYFIN_URL or JELLYFIN_TOKEN is not set.", statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!IsAllowed(path))
                return Results.NotFound();

            await proxy.ForwardAsync(path, context);
            return Results.Empty;
        });
    }

    private async Task ForwardAsync(string path, HttpContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{path}{context.Request.QueryString}");
        foreach (var header in new[] { "Accept", "If-None-Match", "If-Modified-Since" })
            if (context.Request.Headers.TryGetValue(header, out var value))
                request.Headers.TryAddWithoutValidation(header, value.ToArray());

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using (response)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            if (response.Content.Headers.ContentType is { } contentType)
                context.Response.ContentType = contentType.ToString();
            foreach (var header in ForwardedResponseHeaders)
                if (response.Headers.TryGetValues(header, out var values) || response.Content.Headers.TryGetValues(header, out values))
                    context.Response.Headers[header] = values.ToArray();

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}
