using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Threading;

namespace ClaudeMonitor.Services;

public sealed class UpdateChecker : IDisposable
{
    private readonly DispatcherTimer _timer;
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
        var startupDelay = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, async (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            await CheckAsync();
            _timer.Start();
        }, dispatcher);
        startupDelay.Start();
    }

    public event Action? NoUpdateAvailable;

    public async Task CheckAsync()
    {
        try
        {
            var token = LoadToken();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_repo}/releases/latest");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var newVersion = tagName.TrimStart('v');

            if (!IsNewer(newVersion, _currentVersion))
            {
                NoUpdateAvailable?.Invoke();
                return;
            }

            var downloadUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";

            // Find installer asset
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? downloadUrl;
                        break;
                    }
                }
            }

            UpdateAvailable?.Invoke(newVersion, downloadUrl);
        }
        catch { /* silent fail */ }
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
        _timer.Stop();
        _http.Dispose();
    }
}
