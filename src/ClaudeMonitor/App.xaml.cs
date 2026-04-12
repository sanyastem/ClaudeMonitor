using System.IO;
using System.Windows;
using Application = System.Windows.Application;

namespace ClaudeMonitor;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private Mutex? _mutex;
    private Services.UpdateChecker? _updateChecker;
    private bool _manualUpdateCheck;
    private EventHandler? _balloonTipClickedHandler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var ver = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?";
        Services.Log.Info($"Claude Monitor v{ver} starting");

        // Single instance check. Local\ namespace = per-user-session (correct for RDP / fast user
        // switch). AbandonedMutexException is thrown when a prior instance crashed without releasing;
        // we treat it as "we now own the mutex" rather than crashing startup.
        bool isNew;
        try
        {
            _mutex = new Mutex(true, @"Local\ClaudeMonitor_SingleInstance_v1", out isNew);
        }
        catch (AbandonedMutexException)
        {
            Services.Log.Info("Previous instance was abandoned; taking ownership");
            _mutex = new Mutex(true, @"Local\ClaudeMonitor_SingleInstance_v1", out _);
            isNew = true;
        }
        if (!isNew)
        {
            Shutdown();
            return;
        }

        // Catch-all for unhandled exceptions — log them so we have a forensic trail.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Services.Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            Services.Log.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Services.Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        _mainWindow = new MainWindow();

        // First run — show position setup
        var posManager = new Services.PositionManager();
        if (posManager.Load() == null)
        {
            var setup = new SetupWindow();
            if (setup.ShowDialog() == true)
            {
                _mainWindow.ApplySetupPosition(setup.ChosenPosition);
            }
            else
            {
                // User cancelled setup — apply default position anyway
                _mainWindow.ApplySetupPosition(WidgetPosition.TopRight);
            }
        }

        _mainWindow.Show();
        SetupTrayIcon();
        SetupUpdateChecker();
    }

    private void SetupTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip
        {
            Renderer = new Services.DarkMenuRenderer(),
            ShowImageMargin = false,
            Padding = new System.Windows.Forms.Padding(4, 6, 4, 6)
        };

        var settings = new System.Windows.Forms.ToolStripMenuItem("Settings");
        settings.Click += (_, _) =>
        {
            var win = new SettingsWindow(_mainWindow!);
            win.ShowDialog();
        };

        var checkUpdate = new System.Windows.Forms.ToolStripMenuItem("Check for updates");
        checkUpdate.Click += async (_, _) =>
        {
            checkUpdate.Enabled = false;
            checkUpdate.Text = "Checking...";
            _manualUpdateCheck = true;
            await _updateChecker!.CheckAsync();
            _manualUpdateCheck = false;
            checkUpdate.Text = "Check for updates";
            checkUpdate.Enabled = true;
        };

        var about = new System.Windows.Forms.ToolStripMenuItem("About");
        about.Click += (_, _) => new AboutWindow().ShowDialog();

        var exit = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();

        menu.Items.Add(settings);
        menu.Items.Add(checkUpdate);
        menu.Items.Add(about);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Claude Monitor",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => new SettingsWindow(_mainWindow!).ShowDialog();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/icon.ico");
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
                return new System.Drawing.Icon(stream, 32, 32);
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    private void SetupUpdateChecker()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        _updateChecker = new Services.UpdateChecker(System.Windows.Threading.Dispatcher.CurrentDispatcher, version);
        _updateChecker.NoUpdateAvailable += () =>
        {
            if (_manualUpdateCheck)
                _trayIcon?.ShowBalloonTip(3000, "Claude Monitor", "You're up to date!", System.Windows.Forms.ToolTipIcon.Info);
        };

        _updateChecker.CheckFailed += msg =>
        {
            if (_manualUpdateCheck)
                _trayIcon?.ShowBalloonTip(5000, "Update check failed", msg, System.Windows.Forms.ToolTipIcon.Warning);
        };

        _updateChecker.UpdateAvailable += (newVersion, downloadUrl, expectedSha) =>
        {
            _trayIcon?.ShowBalloonTip(
                5000,
                "Claude Monitor Update",
                $"Version {newVersion} is available. Click to install.",
                System.Windows.Forms.ToolTipIcon.Info);

            // Unsubscribe previous handler to avoid leak
            if (_balloonTipClickedHandler != null)
                _trayIcon!.BalloonTipClicked -= _balloonTipClickedHandler;

            _balloonTipClickedHandler = async (_, _) => await DownloadAndInstall(downloadUrl, expectedSha);
            _trayIcon!.BalloonTipClicked += _balloonTipClickedHandler;
        };
    }

    private System.Threading.CancellationTokenSource? _downloadCts;

    private async Task DownloadAndInstall(string assetApiUrl, string expectedSha256)
    {
        string? tempPath = null;
        _downloadCts = new System.Threading.CancellationTokenSource();
        var ct = _downloadCts.Token;
        try
        {
            if (!Services.UpdateChecker.IsTrustedAssetUrl(assetApiUrl))
            {
                Services.Log.Error($"Refusing to download from untrusted URL: {assetApiUrl}");
                _trayIcon?.ShowBalloonTip(5000, "Update blocked", "Update URL is not trusted.", System.Windows.Forms.ToolTipIcon.Error);
                return;
            }
            if (string.IsNullOrEmpty(expectedSha256) || expectedSha256.Length != 64)
            {
                Services.Log.Error("Refusing to download: no expected SHA-256 digest");
                _trayIcon?.ShowBalloonTip(5000, "Update blocked", "Release is missing an integrity digest.", System.Windows.Forms.ToolTipIcon.Error);
                return;
            }

            Services.Log.Info($"Downloading update from: {assetApiUrl}");
            _trayIcon?.ShowBalloonTip(3000, "Claude Monitor", "Downloading update...", System.Windows.Forms.ToolTipIcon.Info);

            var token = GetGhToken();
            var downloader = new Services.AssetDownloader();
            var progress = new Progress<(long read, long? total)>(p =>
            {
                if (p.total is long t && t > 0)
                {
                    var pct = (int)(p.read * 100L / t);
                    if (pct % 25 == 0)
                        Services.Log.Info($"Download progress: {pct}% ({p.read}/{t} bytes)");
                }
            });
            using var bytes = await downloader.DownloadAsync(assetApiUrl, token, progress, ct);

            tempPath = Path.Combine(Path.GetTempPath(), $"ClaudeMonitor-Setup-{Guid.NewGuid():N}.exe");
            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                bytes.Position = 0;
                await bytes.CopyToAsync(fs, ct);
            }
            Services.Log.Info($"Downloaded {bytes.Length} bytes to {tempPath}");

            // Integrity check (fail-closed): SHA-256 must match the digest from the release body.
            var actualSha = await ComputeSha256(tempPath);
            if (!string.Equals(actualSha, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                Services.Log.Error($"SHA-256 mismatch. expected={expectedSha256} actual={actualSha}");
                _trayIcon?.ShowBalloonTip(5000, "Update blocked", "Downloaded installer failed integrity check.", System.Windows.Forms.ToolTipIcon.Error);
                return;
            }
            Services.Log.Info("SHA-256 verified");

            // Authenticode check (defense-in-depth, advisory): logs signer if signed, does not block unsigned.
            if (Services.SignatureVerifier.VerifyAuthenticode(tempPath, out var signer))
                Services.Log.Info($"Authenticode verified, signer: {signer ?? "(unknown)"}");
            else
                Services.Log.Info("Installer is not Authenticode-signed; relying on SHA-256 digest only");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });

            Services.Log.Info("Installer launched, exiting");
            ExitApp();
        }
        catch (Exception ex)
        {
            Services.Log.Error("Download failed", ex);
            _trayIcon?.ShowBalloonTip(5000, "Update failed", $"Could not download update: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
            // Best-effort cleanup of a partial download
            if (tempPath != null) { try { File.Delete(tempPath); } catch { } }
        }
    }

    private static async Task<string> ComputeSha256(string path)
    {
        await using var fs = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? GetGhToken()
    {
        // Dedicated token file
        var tokenFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "widget-gh-token.txt");
        try { if (File.Exists(tokenFile)) return File.ReadAllText(tokenFile).Trim(); } catch { }

        // gh auth token command
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

    private void ExitApp()
    {
        try { _downloadCts?.Cancel(); } catch { }
        if (_balloonTipClickedHandler != null && _trayIcon != null)
        {
            try { _trayIcon.BalloonTipClicked -= _balloonTipClickedHandler; } catch { }
            _balloonTipClickedHandler = null;
        }
        _updateChecker?.Dispose();
        _trayIcon?.Dispose();
        _trayIcon = null;
        _mainWindow?.Close();
        ReleaseMutex();
        Shutdown();
    }

    private void ReleaseMutex()
    {
        if (_mutex == null) return;
        try { _mutex.ReleaseMutex(); } catch { }
        try { _mutex.Dispose(); } catch { }
        _mutex = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        ReleaseMutex();
        base.OnExit(e);
    }
}
