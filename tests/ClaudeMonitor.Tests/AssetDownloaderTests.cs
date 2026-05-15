using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ClaudeMonitor.Services;

namespace ClaudeMonitor.Tests;

public class AssetDownloaderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public Queue<HttpResponseMessage> Responses { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private static HttpResponseMessage Redirect(string to)
    {
        var r = new HttpResponseMessage(HttpStatusCode.Found);
        r.Headers.Location = new Uri(to);
        return r;
    }

    private static HttpResponseMessage Ok(byte[] body)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK);
        r.Content = new ByteArrayContent(body);
        return r;
    }

    [Fact]
    public async Task BearerToken_IsAttached_OnApiGithubHost()
    {
        var handler = new FakeHandler();
        handler.Responses.Enqueue(Ok(new byte[] { 1, 2, 3 }));
        var d = new AssetDownloader(handler);

        await d.DownloadAsync("https://api.github.com/repos/x/y/releases/assets/1", "secret-token");

        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task BearerToken_IsDropped_OnCrossHostRedirect()
    {
        var handler = new FakeHandler();
        handler.Responses.Enqueue(Redirect("https://objects.githubusercontent.com/blob/123"));
        handler.Responses.Enqueue(Ok(new byte[] { 1, 2, 3 }));
        var d = new AssetDownloader(handler);

        await d.DownloadAsync("https://api.github.com/repos/x/y/releases/assets/1", "secret-token");

        Assert.Equal("api.github.com", handler.Requests[0].RequestUri!.Host);
        Assert.NotNull(handler.Requests[0].Headers.Authorization);
        Assert.Equal("objects.githubusercontent.com", handler.Requests[1].RequestUri!.Host);
        Assert.Null(handler.Requests[1].Headers.Authorization);
    }

    [Fact]
    public async Task UntrustedRedirectHost_IsRejected()
    {
        var handler = new FakeHandler();
        handler.Responses.Enqueue(Redirect("https://evil.example.com/payload"));
        var d = new AssetDownloader(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.DownloadAsync("https://api.github.com/repos/x/y/releases/assets/1", "tok"));
    }

    [Fact]
    public async Task NonHttpsRedirect_IsRejected()
    {
        var handler = new FakeHandler();
        handler.Responses.Enqueue(Redirect("http://api.github.com/x"));
        var d = new AssetDownloader(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.DownloadAsync("https://api.github.com/repos/x/y/releases/assets/1", "tok"));
    }

    [Fact]
    public async Task TooManyRedirects_AreRejected()
    {
        var handler = new FakeHandler();
        for (var i = 0; i < AssetDownloader.MaxRedirects + 1; i++)
            handler.Responses.Enqueue(Redirect("https://objects.githubusercontent.com/loop"));
        var d = new AssetDownloader(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.DownloadAsync("https://api.github.com/repos/x/y/releases/assets/1", "tok"));
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _onReport;
        public SyncProgress(Action<T> onReport) => _onReport = onReport;
        public void Report(T value) => _onReport(value);
    }

    [Fact]
    public async Task ProgressIsReported_DuringDownload()
    {
        var payload = new byte[200000];
        Random.Shared.NextBytes(payload);
        var handler = new FakeHandler();
        var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        ok.Content.Headers.ContentLength = payload.Length;
        handler.Responses.Enqueue(ok);
        var d = new AssetDownloader(handler);

        var reports = new List<(long, long?)>();
        // Synchronous IProgress so we don't race a thread-pool dispatch from Progress<T>.
        var p = new SyncProgress<(long, long?)>(reports.Add);
        var ms = await d.DownloadAsync("https://api.github.com/x", null, p);

        Assert.Equal(payload.Length, ms.Length);
        Assert.NotEmpty(reports);
        Assert.Equal(payload.Length, reports[^1].Item1);
        Assert.Equal(payload.Length, reports[^1].Item2);
    }

    [Fact]
    public void TrustedHosts_AreExactlyThree()
    {
        Assert.Equal(3, AssetDownloader.TrustedHosts.Count);
        Assert.Contains("api.github.com", AssetDownloader.TrustedHosts);
        Assert.Contains("objects.githubusercontent.com", AssetDownloader.TrustedHosts);
        Assert.Contains("release-assets.githubusercontent.com", AssetDownloader.TrustedHosts);
    }
}
