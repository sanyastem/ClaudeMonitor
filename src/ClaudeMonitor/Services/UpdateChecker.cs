using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

    public event Action<string, string>? UpdateAvailable; // (newVersion, downloadUrl)

    public UpdateChecker(Dispatcher dispatcher, string currentVersion, string repo = "sanyastem/ClaudeMonitor")
    {
        _currentVersion = currentVersion;
        _repo = repo;
        _tokenFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "widget-gh-token.txt");

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClaudeMonitor", currentVersion));

        _timer = new DispatcherTimer(TimeSpan.FromHours(12), DispatcherPriority.Background, async (_, _) => await CheckAsync(), dispatcher);

        // Check after 30 seconds on startup, then every 12 hours
        _startupDelay = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, async (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            await CheckAsync();
            _timer.Start();
        }, dispatcher);
        _startupDelay.Start();
    }

    public event Action? NoUpdateAvailable;

    public async Task CheckAsync()
    {
        try
        {
            var token = LoadToken();
            Log.Info($"Update check started. Current: {_currentVersion}, token: {(string.IsNullOrEmpty(token) ? "none" : "found")}");

            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_repo}/releases/latest");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Update check failed: HTTP {(int)response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var newVersion = tagName.TrimStart('v');
            Log.Info($"Latest release: {newVersion}");

            if (!IsNewer(newVersion, _currentVersion))
            {
                Log.Info("No update available");
                NoUpdateAvailable?.Invoke();
                return;
            }

            var downloadUrl = "";

            // For private repos: use API asset URL (not browser_download_url which returns 404)
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameProp))
                        continue;
                    var name = nameProp.GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("url", out var urlProp))
                            downloadUrl = urlProp.GetString() ?? "";
                        Log.Info($"Asset found: {name}, URL: {downloadUrl}");
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log.Error("No .exe asset found in release");
                return;
            }

            UpdateAvailable?.Invoke(newVersion, downloadUrl);
        }
        catch (Exception ex)
        {
            Log.Error("Update check exception", ex);
        }
    }

    private string? LoadToken()
    {
        // Try dedicated token file first
        try
        {
            if (File.Exists(_tokenFile))
                return File.ReadAllText(_tokenFile).Trim();
        }
        catch { }

        // Try gh CLI token from hosts.yml
        try
        {
            var ghHosts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GitHub CLI", "hosts.yml");
            if (File.Exists(ghHosts))
            {
                var lines = File.ReadAllLines(ghHosts);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("oauth_token:"))
                        return trimmed["oauth_token:".Length..].Trim();
                }
            }
        }
        catch { }

        // Try running `gh auth token` command
        try
        {
            var ghExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe");
            if (!File.Exists(ghExe)) ghExe = "gh";
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
                proc.WaitForExit(3000);
                if (!string.IsNullOrEmpty(token) && !token.Contains(' '))
                    return token;
            }
        }
        catch { }

        return null;
    }

    private static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(remote, out var rv) && Version.TryParse(local, out var lv))
            return rv > lv;
        return false;
    }

    public void Dispose()
    {
        _startupDelay?.Stop();
        _startupDelay = null;
        _timer.Stop();
        _http.Dispose();
    }
}
