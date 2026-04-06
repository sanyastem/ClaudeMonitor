using System.IO;
using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace ClaudeMonitor;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private Mutex? _mutex;
    private Services.UpdateChecker? _updateChecker;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        }

        _mainWindow.Show();
        SetupTrayIcon();
        SetupUpdateChecker();
    }

    private void SetupTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var showHide = new System.Windows.Forms.ToolStripMenuItem("Show/Hide");
        showHide.Click += (_, _) => ToggleWindow();

        var alwaysVisible = new System.Windows.Forms.ToolStripMenuItem("Always visible");
        alwaysVisible.Checked = _mainWindow!.AlwaysVisible;
        alwaysVisible.Click += (_, _) =>
        {
            _mainWindow.AlwaysVisible = !_mainWindow.AlwaysVisible;
            alwaysVisible.Checked = _mainWindow.AlwaysVisible;
        };

        var onTop = new System.Windows.Forms.ToolStripMenuItem("Always on top");
        onTop.Checked = _mainWindow!.AlwaysOnTop;
        onTop.Click += (_, _) =>
        {
            _mainWindow.AlwaysOnTop = !_mainWindow.AlwaysOnTop;
            onTop.Checked = _mainWindow.AlwaysOnTop;
        };

        var showLimits = new System.Windows.Forms.ToolStripMenuItem("Show limits when idle");
        showLimits.Checked = _mainWindow.ShowIdleLimits;
        showLimits.Click += (_, _) =>
        {
            _mainWindow.ShowIdleLimits = !_mainWindow.ShowIdleLimits;
            showLimits.Checked = _mainWindow.ShowIdleLimits;
        };

        var autostart = new System.Windows.Forms.ToolStripMenuItem("Autostart");
        autostart.Checked = IsAutostartEnabled();
        autostart.Click += (_, _) =>
        {
            var enable = !IsAutostartEnabled();
            SetAutostart(enable);
            autostart.Checked = enable;
        };

        var exit = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();

        menu.Items.Add(showHide);
        menu.Items.Add(alwaysVisible);
        menu.Items.Add(onTop);
        menu.Items.Add(showLimits);
        menu.Items.Add(autostart);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Claude Monitor",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => ToggleWindow();
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

    private void ToggleWindow()
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Topmost = true;
        }
    }

    private static bool IsAutostartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("ClaudeMonitor") != null;
        }
        catch { return false; }
    }

    private static void SetAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? "";
                key.SetValue("ClaudeMonitor", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("ClaudeMonitor", false);
            }
        }
        catch { }
    }

    private void SetupUpdateChecker()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        _updateChecker = new Services.UpdateChecker(System.Windows.Threading.Dispatcher.CurrentDispatcher, version);
        _updateChecker.UpdateAvailable += (newVersion, downloadUrl) =>
        {
            _trayIcon?.ShowBalloonTip(
                5000,
                "Claude Monitor Update",
                $"Version {newVersion} is available. Click to install.",
                System.Windows.Forms.ToolTipIcon.Info);

            _trayIcon!.BalloonTipClicked += async (_, _) => await DownloadAndInstall(downloadUrl);
        };
    }

    private async Task DownloadAndInstall(string downloadUrl)
    {
        try
        {
            _trayIcon?.ShowBalloonTip(3000, "Claude Monitor", "Downloading update...", System.Windows.Forms.ToolTipIcon.Info);

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeMonitor");

            // For private repos, try gh token
            var tokenFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "widget-gh-token.txt");
            if (File.Exists(tokenFile))
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", File.ReadAllText(tokenFile).Trim());
            else
            {
                var ghHosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHub CLI", "hosts.yml");
                if (File.Exists(ghHosts))
                {
                    foreach (var line in File.ReadAllLines(ghHosts))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("oauth_token:"))
                        {
                            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t["oauth_token:".Length..].Trim());
                            break;
                        }
                    }
                }
            }
            http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));

            var bytes = await http.GetByteArrayAsync(downloadUrl);
            var tempPath = Path.Combine(Path.GetTempPath(), "ClaudeMonitor-Setup.exe");
            await File.WriteAllBytesAsync(tempPath, bytes);

            // Launch installer silently and exit current instance
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });

            ExitApp();
        }
        catch (Exception ex)
        {
            _trayIcon?.ShowBalloonTip(5000, "Update failed", $"Could not download update: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
        }
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
