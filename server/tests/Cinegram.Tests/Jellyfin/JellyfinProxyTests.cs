using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Jellyfin;

namespace Cinegram.Tests.Jellyfin;

public class JellyfinProxyTests : IAsyncDisposable
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"Items":[]}""", System.Text.Encoding.UTF8, "application/json"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(request));
        }
    }

    private readonly RecordingHandler _jellyfin = new();
    private WebApplication? _app;

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    private async Task<HttpClient> StartAsync(string? url = "http://jellyfin:8096/", string? token = "server-token")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(new JellyfinProxy(url, token, _jellyfin));
        _app = builder.Build();
        JellyfinProxy.Map(_app.MapGroup("/api"));
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    [Fact]
    public async Task Get_ForwardsPathAndQueryWithTheServerToken()
    {
        using var http = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/jellyfin/Shows/abc123/Episodes?seasonId=def&Fields=Path,Overview");
        request.Headers.Authorization = new AuthenticationHeaderValue("MediaBrowser", "Token=\"browser-token\"");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"Items":[]}""", await response.Content.ReadAsStringAsync());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var forwarded = Assert.Single(_jellyfin.Requests);
        Assert.Equal("http://jellyfin:8096/Shows/abc123/Episodes?seasonId=def&Fields=Path,Overview", forwarded.RequestUri!.ToString());
        Assert.Equal("MediaBrowser", forwarded.Headers.Authorization?.Scheme);
        Assert.Equal("Token=\"server-token\"", forwarded.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Get_DoesNotForwardLegacyAuthenticationFromTheBrowser()
    {
        using var http = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/jellyfin/Users");
        request.Headers.Add("X-Emby-Token", "browser-token");
        request.Headers.Add("X-MediaBrowser-Token", "browser-token");
        request.Headers.Add("X-Emby-Authorization", "MediaBrowser Token=\"browser-token\"");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var forwarded = Assert.Single(_jellyfin.Requests);
        Assert.False(forwarded.Headers.Contains("X-Emby-Token"));
        Assert.False(forwarded.Headers.Contains("X-MediaBrowser-Token"));
        Assert.False(forwarded.Headers.Contains("X-Emby-Authorization"));
        Assert.Equal("Token=\"server-token\"", forwarded.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Get_PassesThroughStatusAndCacheHeaders()
    {
        _jellyfin.Respond = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            response.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromDays(365) };
            return response;
        };
        using var http = await StartAsync();

        using var response = await http.GetAsync("/api/jellyfin/Items/abc/Images/Primary?tag=t&maxWidth=400");

        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
    }

    [Theory]
    [InlineData("/api/jellyfin/System/Configuration")]
    [InlineData("/api/jellyfin/Users/../System/Configuration")]
    [InlineData("/api/jellyfin/Users/%2E%2E/System/Configuration")]
    [InlineData("/api/jellyfin/Users%2F..%2FSystem%2FConfiguration")]
    [InlineData("/api/jellyfin/Items/a.b")]
    public async Task Get_RejectsPathsOutsideTheReadEndpoints(string path)
    {
        using var http = await StartAsync();

        using var response = await http.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_jellyfin.Requests);
    }

    [Fact]
    public async Task Post_IsNotForwarded()
    {
        using var http = await StartAsync();

        using var response = await http.PostAsync("/api/jellyfin/Items/abc", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Empty(_jellyfin.Requests);
    }

    [Fact]
    public async Task Get_ReportsAMissingConfiguration()
    {
        using var http = await StartAsync(token: null);

        using var proxied = await http.GetAsync("/api/jellyfin/Users");
        using var status = await http.GetAsync("/api/jellyfin");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, proxied.StatusCode);
        using var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.Equal("http://jellyfin:8096", json.RootElement.GetProperty("url").GetString());
        Assert.False(json.RootElement.GetProperty("configured").GetBoolean());
        Assert.Empty(_jellyfin.Requests);
    }

    [Fact]
    public async Task Get_AnswersBadGatewayWhenJellyfinIsDown()
    {
        _jellyfin.Respond = _ => throw new HttpRequestException("connection refused");
        using var http = await StartAsync();

        using var response = await http.GetAsync("/api/jellyfin/Users");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
