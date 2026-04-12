using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;

namespace ClaudeMonitor.Services;

public sealed class PositionManager
{
    private readonly string _posFile;
    private readonly string _settingsFile;

    public PositionManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
        _posFile = Path.Combine(dir, "widget-pos.json");
        _settingsFile = Path.Combine(dir, "widget-settings.json");
        _lastLimitsFile = Path.Combine(dir, "widget-last-limits.json");
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
            using var doc = JsonDocument.Parse(File.ReadAllText(_posFile));
            var left = doc.RootElement.GetProperty("Left").GetDouble();
            var top = doc.RootElement.GetProperty("Top").GetDouble();
            return (left, top);
        }
        catch { return null; }
    }

    private Dictionary<string, object> ReadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFile)) return new();
            using var doc = JsonDocument.Parse(File.ReadAllText(_settingsFile));
            var dict = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ToString() ?? ""
                };
            }
            return dict;
        }
        catch { return new(); }
    }

    private void WriteSetting(string key, object value)
    {
        try
        {
            var settings = ReadSettings();
            settings[key] = value;
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    private bool GetBool(string key, bool defaultValue = false)
    {
        var settings = ReadSettings();
        return settings.TryGetValue(key, out var v) && v is bool b ? b : defaultValue;
    }

    public bool AlwaysVisible
    {
        get => GetBool("AlwaysVisible");
        set => WriteSetting("AlwaysVisible", value);
    }

    public bool AlwaysOnTop
    {
        get => GetBool("AlwaysOnTop", false);
        set => WriteSetting("AlwaysOnTop", value);
    }

    // Statusline element toggles (all on by default)
    public bool SlModel { get => GetBool("sl_model", true); set => WriteSetting("sl_model", value); }
    public bool SlContext { get => GetBool("sl_context", true); set => WriteSetting("sl_context", value); }
    public bool SlCost { get => GetBool("sl_cost", true); set => WriteSetting("sl_cost", value); }
    public bool SlTime { get => GetBool("sl_time", true); set => WriteSetting("sl_time", value); }
    public bool SlTokens { get => GetBool("sl_tokens", true); set => WriteSetting("sl_tokens", value); }
    public bool SlLines { get => GetBool("sl_lines", true); set => WriteSetting("sl_lines", value); }
    public bool SlLimits { get => GetBool("sl_limits", true); set => WriteSetting("sl_limits", value); }

    public bool ShowIdleLimits
    {
        get => GetBool("ShowIdleLimits", true);
        set => WriteSetting("ShowIdleLimits", value);
    }

    private readonly string _lastLimitsFile;

    public void SaveLastLimits(double rl5, long rl5Reset, double rl7, long rl7Reset)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { Rl5 = rl5, Rl5Reset = rl5Reset, Rl7 = rl7, Rl7Reset = rl7Reset });
            File.WriteAllText(_lastLimitsFile, json);
        }
        catch { }
    }

    public (double rl5, long rl5Reset, double rl7, long rl7Reset)? LoadLastLimits()
    {
        try
        {
            if (!File.Exists(_lastLimitsFile)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(_lastLimitsFile));
            return (
                doc.RootElement.GetProperty("Rl5").GetDouble(),
                doc.RootElement.GetProperty("Rl5Reset").GetInt64(),
                doc.RootElement.GetProperty("Rl7").GetDouble(),
                doc.RootElement.GetProperty("Rl7Reset").GetInt64()
            );
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
