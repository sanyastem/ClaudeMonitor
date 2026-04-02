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
        _mainWindow.Show();

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var showHide = new System.Windows.Forms.ToolStripMenuItem("Show/Hide");
        showHide.Click += (_, _) => ToggleWindow();

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
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                var iconPath = Path.Combine(Path.GetDirectoryName(exePath)!, "icon.ico");
                if (File.Exists(iconPath))
                    return new System.Drawing.Icon(iconPath);
            }
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

    private void ExitApp()
    {
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
