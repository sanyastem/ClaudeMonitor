using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;
using ClaudeMonitor.Services;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ClaudeMonitor;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly PositionManager _pm;

    public SettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _pm = new PositionManager();
        RefreshDots();
    }

    private void RefreshDots()
    {
        SetDot(dotAlwaysVisible, _mainWindow.AlwaysVisible);
        SetDot(dotAlwaysOnTop, _mainWindow.AlwaysOnTop);
        SetDot(dotShowIdleLimits, _mainWindow.ShowIdleLimits);
        SetDot(dotAutostart, IsAutostartEnabled());

        // Statusline dots (smaller)
        SetSmallDot(dotSlModel, _pm.SlModel);
        SetSmallDot(dotSlContext, _pm.SlContext);
        SetSmallDot(dotSlCost, _pm.SlCost);
        SetSmallDot(dotSlTime, _pm.SlTime);
        SetSmallDot(dotSlTokens, _pm.SlTokens);
        SetSmallDot(dotSlLines, _pm.SlLines);
        SetSmallDot(dotSlLimits, _pm.SlLimits);
    }

    private static void SetDot(Ellipse dot, bool on)
    {
        if (on)
        {
            dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ade80"));
            dot.Stroke = null;
        }
        else
        {
            dot.Fill = Brushes.Transparent;
            dot.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
            dot.StrokeThickness = 1.5;
        }
        dot.Width = 10; dot.Height = 10;
        dot.Margin = new Thickness(0, 0, 10, 0);
    }

    private static void SetSmallDot(Ellipse dot, bool on)
    {
        if (on)
        {
            dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ade80"));
            dot.Stroke = null;
        }
        else
        {
            dot.Fill = Brushes.Transparent;
            dot.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
            dot.StrokeThickness = 1.2;
        }
    }

    private void Toggle_AlwaysVisible(object sender, MouseButtonEventArgs e) { _mainWindow.AlwaysVisible = !_mainWindow.AlwaysVisible; RefreshDots(); }
    private void Toggle_AlwaysOnTop(object sender, MouseButtonEventArgs e) { _mainWindow.AlwaysOnTop = !_mainWindow.AlwaysOnTop; RefreshDots(); }
    private void Toggle_ShowIdleLimits(object sender, MouseButtonEventArgs e) { _mainWindow.ShowIdleLimits = !_mainWindow.ShowIdleLimits; RefreshDots(); }
    private void Toggle_Autostart(object sender, MouseButtonEventArgs e) { SetAutostart(!IsAutostartEnabled()); RefreshDots(); }

    private void Toggle_SlModel(object sender, MouseButtonEventArgs e) { _pm.SlModel = !_pm.SlModel; RefreshDots(); }
    private void Toggle_SlContext(object sender, MouseButtonEventArgs e) { _pm.SlContext = !_pm.SlContext; RefreshDots(); }
    private void Toggle_SlCost(object sender, MouseButtonEventArgs e) { _pm.SlCost = !_pm.SlCost; RefreshDots(); }
    private void Toggle_SlTime(object sender, MouseButtonEventArgs e) { _pm.SlTime = !_pm.SlTime; RefreshDots(); }
    private void Toggle_SlTokens(object sender, MouseButtonEventArgs e) { _pm.SlTokens = !_pm.SlTokens; RefreshDots(); }
    private void Toggle_SlLines(object sender, MouseButtonEventArgs e) { _pm.SlLines = !_pm.SlLines; RefreshDots(); }
    private void Toggle_SlLimits(object sender, MouseButtonEventArgs e) { _pm.SlLimits = !_pm.SlLimits; RefreshDots(); }

    private void Close_Click(object sender, MouseButtonEventArgs e) => Close();
    private void Window_Drag(object sender, MouseButtonEventArgs e) => DragMove();

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
                key.SetValue("ClaudeMonitor", $"\"{Environment.ProcessPath ?? ""}\"");
            else
                key.DeleteValue("ClaudeMonitor", false);
        }
        catch { }
    }
}
