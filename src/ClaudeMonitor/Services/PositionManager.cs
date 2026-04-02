using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;

namespace ClaudeMonitor.Services;

public sealed class PositionManager
{
    private readonly string _posFile;

    public PositionManager()
    {
        _posFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "widget-pos.json");
    }

    public void Save(double left, double top)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { Left = left, Top = top });
            File.WriteAllText(_posFile, json);
        }
        catch { }
    }

    public (double left, double top)? Load()
    {
        try
        {
            if (!File.Exists(_posFile)) return null;
            var doc = JsonDocument.Parse(File.ReadAllText(_posFile));
            var left = doc.RootElement.GetProperty("Left").GetDouble();
            var top = doc.RootElement.GetProperty("Top").GetDouble();
            return (left, top);
        }
        catch { return null; }
    }

    public void ApplyDefaultPosition(Window window)
    {
        var saved = Load();
        if (saved.HasValue)
        {
            window.Left = saved.Value.left;
            window.Top = saved.Value.top;
        }
        else
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            window.Left = area.Right - window.Width - 20;
            window.Top = area.Top + 20;
        }
    }

    public void EnsureOnScreen(Window window)
    {
        var vScreen = SystemInformation.VirtualScreen;
        var moved = false;

        // Completely off-screen — reset to primary
        if (window.Left + window.Width < vScreen.Left ||
            window.Left > vScreen.Right ||
            window.Top + window.ActualHeight < vScreen.Top ||
            window.Top > vScreen.Bottom)
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            window.Left = area.Right - window.Width - 20;
            window.Top = area.Top + 20;
            moved = true;
        }

        // Clamp edges
        if (window.Left < vScreen.Left) { window.Left = vScreen.Left + 5; moved = true; }
        if (window.Top < vScreen.Top) { window.Top = vScreen.Top + 5; moved = true; }
        if (window.Left + window.Width > vScreen.Right) { window.Left = vScreen.Right - window.Width - 5; moved = true; }
        if (window.Top + window.ActualHeight > vScreen.Bottom) { window.Top = vScreen.Bottom - window.ActualHeight - 5; moved = true; }

        if (moved) Save(window.Left, window.Top);
    }
}
