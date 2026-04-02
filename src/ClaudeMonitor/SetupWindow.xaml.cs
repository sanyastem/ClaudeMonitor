using System.Windows;
using System.Windows.Forms;

namespace ClaudeMonitor;

public enum WidgetPosition { TopLeft, TopRight, BottomLeft, BottomRight }

public partial class SetupWindow : Window
{
    public WidgetPosition ChosenPosition { get; private set; } = WidgetPosition.TopRight;

    public SetupWindow()
    {
        InitializeComponent();
    }

    private void Position_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            ChosenPosition = tag switch
            {
                "TopLeft" => WidgetPosition.TopLeft,
                "TopRight" => WidgetPosition.TopRight,
                "BottomLeft" => WidgetPosition.BottomLeft,
                "BottomRight" => WidgetPosition.BottomRight,
                _ => WidgetPosition.TopRight
            };
        }
        DialogResult = true;
        Close();
    }

    public static (double left, double top) CalculatePosition(WidgetPosition pos, double widgetWidth, double widgetHeight)
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        return pos switch
        {
            WidgetPosition.TopLeft => (area.Left + 20, area.Top + 20),
            WidgetPosition.TopRight => (area.Right - widgetWidth - 20, area.Top + 20),
            WidgetPosition.BottomLeft => (area.Left + 20, area.Bottom - widgetHeight - 20),
            WidgetPosition.BottomRight => (area.Right - widgetWidth - 20, area.Bottom - widgetHeight - 20),
            _ => (area.Right - widgetWidth - 20, area.Top + 20)
        };
    }
}
