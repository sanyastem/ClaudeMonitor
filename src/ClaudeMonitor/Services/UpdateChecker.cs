using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;

namespace ClaudeMonitor.Services;

public sealed class UpdateChecker : IDisposable
{
    private readonly DispatcherTimer _timer;
    private DispatcherTimer? _startupDelay;
    private readonly HttpClient _http;
    private readonly string _currentVersion;
    private readonly string _repo;
    private readonly string _tokenFile;
    private readonly CancellationTokenSource _cts = new();

    public event Action<string, string, string>? UpdateAvailable; // (newVersion, downloadUrl, expectedSha256)
    public event Action? NoUpdateAvailable;
    public event Action<string>? CheckFailed; // message — surfaced to the user on manual check

    public UpdateChecker(Dispatcher dispatcher, string currentVersion, string repo = "sanyastem/ClaudeMonitor")
    {
        _currentVersion = currentVersion;
        _repo = repo;
        _tokenFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "widget-gh-token.txt");

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClaudeMonitor", currentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        _timer = new DispatcherTimer(TimeSpan.FromHours(12), DispatcherPriority.Background, async (_, _) => await CheckAsync(), dispatcher);

        _startupDelay = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, async (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            await CheckAsync();
            _timer.Start();
        }, dispatcher);
        _startupDelay.Start();
    }

    public async Task CheckAsync()
    {
        try
        {
            // SECURITY: never log the token value itself — only "found"/"none".
            var token = LoadToken();
            Log.Info($"Update check started. Current: {_currentVersion}, token: {(string.IsNullOrEmpty(token) ? "none" : "found")}");

            // Primary: /releases/latest. On 404 fall back to scanning /releases for the highest stable.
            var release = await FetchReleaseWithRetry($"https://api.github.com/repos/{_repo}/releases/latest", token, _cts.Token);
            if (release == null)
            {
                Log.Info("Falling back to /releases list");
                release = await FindLatestStable(token, _cts.Token);
            }
            if (release == null)
            {
                CheckFailed?.Invoke("Could not reach GitHub.");
                return;
            }

            using (release)
            {
                var tagName = release.RootElement.TryGetProperty("tag_name", out var tn) ? tn.GetString() ?? "" : "";
                var newVersion = tagName.TrimStart('v');
                Log.Info($"Latest release: {newVersion}");

                if (!IsNewer(newVersion, _currentVersion))
                {
                    Log.Info("No update available");
                    NoUpdateAvailable?.Invoke();
                    return;
                }

                var downloadUrl = "";
                if (release.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (!asset.TryGetProperty("name", out var nameProp)) continue;
                        var name = nameProp.GetString() ?? "";
                        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (asset.TryGetProperty("url", out var urlProp))
                        {
                            var rawUrl = urlProp.GetString() ?? "";
                            if (IsTrustedAssetUrl(rawUrl))
                            {
                                downloadUrl = rawUrl;
                                Log.Info($"Asset found: {name}");
                            }
                            else
                            {
                                Log.Error($"Rejected asset URL with untrusted host: {rawUrl}");
                            }
                        }
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Log.Error("No trusted .exe asset found in release");
                    return;
                }

                var body = release.RootElement.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                var expectedSha = ExtractSha256(body);
                if (string.IsNullOrEmpty(expectedSha))
                {
                    Log.Error("Release body does not contain a SHA256 digest for the installer; refusing to advertise update");
                    return;
                }

                UpdateAvailable?.Invoke(newVersion, downloadUrl, expectedSha);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Update check exception", ex);
            CheckFailed?.Invoke(ex.Message);
        }
    }

    private async Task<JsonDocument?> FetchReleaseWithRetry(string url, string? token, CancellationToken ct)
    {
        var delays = new[] { 2_000, 8_000, 30_000 };
        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Info($"404 from {url}");
                    return null; // Trigger fallback in caller
                }
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"Update check failed: HTTP {(int)response.StatusCode}");
                    if ((int)response.StatusCode < 500) return null; // Don't retry on 4xx
                    if (attempt == delays.Length - 1) return null;
                    await Task.Delay(delays[attempt], ct).ConfigureAwait(false);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonDocument.Parse(json);
            }
            catch (TaskCanceledException) { throw; }
            catch (HttpRequestException ex)
            {
                Log.Error($"Update check network error (attempt {attempt + 1})", ex);
                if (attempt == delays.Length - 1) return null;
                await Task.Delay(delays[attempt], ct).ConfigureAwait(false);
            }
        }
        return null;
    }

    private async Task<JsonDocument?> FindLatestStable(string? token, CancellationToken ct)
    {
        var list = await FetchReleaseWithRetry($"https://api.github.com/repos/{_repo}/releases?per_page=20", token, ct);
        if (list == null) return null;
        try
        {
            JsonElement? best = null;
            Version? bestVer = null;
            foreach (var rel in list.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (rel.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                var tag = (rel.TryGetProperty("tag_name", out var tn) ? tn.GetString() : null) ?? "";
                if (!Version.TryParse(tag.TrimStart('v'), out var v)) continue;
                if (bestVer == null || v > bestVer) { bestVer = v; best = rel; }
            }
            if (best == null) return null;
            // Re-serialize the chosen element into its own JsonDocument to extend its lifetime.
            return JsonDocument.Parse(best.Value.GetRawText());
        }
        finally { list.Dispose(); }
    }

    private string? LoadToken()
    {
        try { if (File.Exists(_tokenFile)) return File.ReadAllText(_tokenFile).Trim(); } catch { }

        try
        {
            var ghHosts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GitHub CLI", "hosts.yml");
            if (File.Exists(ghHosts))
            {
                foreach (var line in File.ReadAllLines(ghHosts))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("oauth_token:"))
                        return trimmed["oauth_token:".Length..].Trim();
                }
            }
        }
        catch { }

        try
        {
            var ghExe = ResolveGhPath();
            if (ghExe == null) return null;
            var psi = new System.Diagnostics.ProcessStartInfo(ghExe, "auth token")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var token = proc.StandardOutput.ReadToEnd().Trim();
                if (!proc.WaitForExit(3000))
                {
                    try { proc.Kill(); } catch { }
                }
                if (!string.IsNullOrEmpty(token) && !token.Contains(' '))
                    return token;
            }
        }
        catch { }

        return null;
    }

    private static string? ResolveGhPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GitHub CLI", "gh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GitHub CLI", "gh.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var full = Path.Combine(dir, "gh.exe");
                if (File.Exists(full)) return full;
            }
            catch { }
        }
        return null;
    }

    private static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(remote, out var rv) && Version.TryParse(local, out var lv))
            return rv > lv;
        Log.Error($"Version compare failed: remote={remote} local={local}");
        return false;
    }

    public static bool IsTrustedAssetUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly System.Text.RegularExpressions.Regex Sha256Pattern =
        new(@"(?im)^[\s>*\-_`]*sha-?256[\s:=]+`?([0-9a-f]{64})`?\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string ExtractSha256(string releaseBody)
    {
        if (string.IsNullOrEmpty(releaseBody))
            return "";
        var match = Sha256Pattern.Match(releaseBody);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _startupDelay?.Stop();
        _startupDelay = null;
        _timer.Stop();
        _http.Dispose();
        _cts.Dispose();
    }
}
