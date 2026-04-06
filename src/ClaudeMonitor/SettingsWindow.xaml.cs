using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ClaudeMonitor;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;

    public SettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        RefreshDots();
    }

    private void RefreshDots()
    {
        SetDot(dotAlwaysVisible, _mainWindow.AlwaysVisible);
        SetDot(dotAlwaysOnTop, _mainWindow.AlwaysOnTop);
        SetDot(dotShowIdleLimits, _mainWindow.ShowIdleLimits);
        SetDot(dotAutostart, IsAutostartEnabled());
    }

    private static void SetDot(Ellipse dot, bool on)
    {
        if (on)
        {
            dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ade80"));
            dot.Stroke = null;
            dot.Width = 10; dot.Height = 10;
            dot.Margin = new Thickness(0, 0, 10, 0);
        }
        else
        {
            dot.Fill = Brushes.Transparent;
            dot.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
            dot.StrokeThickness = 1.5;
            dot.Width = 10; dot.Height = 10;
            dot.Margin = new Thickness(0, 0, 10, 0);
        }
    }

    private void Toggle_AlwaysVisible(object sender, MouseButtonEventArgs e)
    {
        _mainWindow.AlwaysVisible = !_mainWindow.AlwaysVisible;
        RefreshDots();
    }

    private void Toggle_AlwaysOnTop(object sender, MouseButtonEventArgs e)
    {
        _mainWindow.AlwaysOnTop = !_mainWindow.AlwaysOnTop;
        RefreshDots();
    }

    private void Toggle_ShowIdleLimits(object sender, MouseButtonEventArgs e)
    {
        _mainWindow.ShowIdleLimits = !_mainWindow.ShowIdleLimits;
        RefreshDots();
    }

    private void Toggle_Autostart(object sender, MouseButtonEventArgs e)
    {
        var enable = !IsAutostartEnabled();
        SetAutostart(enable);
        RefreshDots();
    }

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
