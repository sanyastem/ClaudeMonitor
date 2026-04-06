using System.Windows;
using System.Windows.Input;

namespace ClaudeMonitor;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var ver = typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "?";
        tbVersion.Text = $"v{ver}";
    }

    private void Close_Click(object sender, MouseButtonEventArgs e) => Close();
    private void Window_Drag(object sender, MouseButtonEventArgs e) => DragMove();

    private void GitHub_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/sanyastem/ClaudeMonitor") { UseShellExecute = true });
        }
        catch { }
    }
}
