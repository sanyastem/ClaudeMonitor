using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace ClaudeMonitor.Services;

public sealed class AssetDownloader
{
    public static readonly IReadOnlySet<string> TrustedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    };

    public const int MaxRedirects = 5;

    private readonly HttpMessageHandler _handler;

    public AssetDownloader(HttpMessageHandler? handler = null)
    {
        _handler = handler ?? new SocketsHttpHandler { AllowAutoRedirect = false };
    }

    public async Task<MemoryStream> DownloadAsync(
        string startUrl,
        string? token,
        IProgress<(long bytesRead, long? totalBytes)>? progress = null,
        CancellationToken ct = default)
    {
        using var http = new HttpClient(_handler, disposeHandler: false) { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeMonitor");

        var url = startUrl;
        for (var hop = 0; hop < MaxRedirects; hop++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            // Bearer token attached ONLY when the host is api.github.com — never forwarded on a
            // cross-host redirect even if HttpClient defaults change.
            var host = new Uri(url).Host;
            if (!string.IsNullOrEmpty(token) && string.Equals(host, "api.github.com", StringComparison.OrdinalIgnoreCase))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location != null)
            {
                var next = resp.Headers.Location.IsAbsoluteUri
                    ? resp.Headers.Location
                    : new Uri(new Uri(url), resp.Headers.Location);
                if (next.Scheme != Uri.UriSchemeHttps || !TrustedHosts.Contains(next.Host))
                    throw new InvalidOperationException($"Refusing redirect to untrusted host: {next.Host}");
                url = next.ToString();
                continue;
            }

            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength;
            var ms = new MemoryStream(total.HasValue ? (int)total.Value : 1 << 20);
            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                await ms.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                progress?.Report((read, total));
            }
            return ms;
        }
        throw new InvalidOperationException("Too many redirects");
    }
}
