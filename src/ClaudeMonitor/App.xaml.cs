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

        // Single instance check
        _mutex = new Mutex(true, "ClaudeMonitor_SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

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

        _updateChecker.UpdateAvailable += (newVersion, downloadUrl) =>
        {
            _trayIcon?.ShowBalloonTip(
                5000,
                "Claude Monitor Update",
                $"Version {newVersion} is available. Click to install.",
                System.Windows.Forms.ToolTipIcon.Info);

            // Unsubscribe previous handler to avoid leak
            if (_balloonTipClickedHandler != null)
                _trayIcon!.BalloonTipClicked -= _balloonTipClickedHandler;

            _balloonTipClickedHandler = async (_, _) => await DownloadAndInstall(downloadUrl);
            _trayIcon!.BalloonTipClicked += _balloonTipClickedHandler;
        };
    }

    private async Task DownloadAndInstall(string assetApiUrl)
    {
        try
        {
            Services.Log.Info($"Downloading update from: {assetApiUrl}");
            _trayIcon?.ShowBalloonTip(3000, "Claude Monitor", "Downloading update...", System.Windows.Forms.ToolTipIcon.Info);

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeMonitor");
            // Accept octet-stream to get binary from GitHub API asset URL
            http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));

            // Auth token
            var token = GetGhToken();
            if (!string.IsNullOrEmpty(token))
            {
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Services.Log.Info("Using auth token for download");
            }

            var bytes = await http.GetByteArrayAsync(assetApiUrl);
            var tempPath = Path.Combine(Path.GetTempPath(), "ClaudeMonitor-Setup.exe");
            await File.WriteAllBytesAsync(tempPath, bytes);
            Services.Log.Info($"Downloaded {bytes.Length} bytes to {tempPath}");

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
        }
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
        _updateChecker?.Dispose();
        _trayIcon?.Dispose();
        _mainWindow?.Close();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
