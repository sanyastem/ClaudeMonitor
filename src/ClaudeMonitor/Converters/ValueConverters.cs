using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ClaudeMonitor.Converters;

public class ContextPercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct)
            return Math.Round(pct * 2.72); // max ~272px
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct)
        {
            if (pct < 50) return new SolidColorBrush(ColorFromHex("#4ade80"));
            if (pct < 80) return new SolidColorBrush(ColorFromHex("#facc15"));
            return new SolidColorBrush(ColorFromHex("#f87171"));
        }
        return new SolidColorBrush(ColorFromHex("#666666"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Color ColorFromHex(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}

public class ContextPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct)
        {
            Color c1, c2;
            if (pct < 50) { c1 = ColorFromHex("#22d3ee"); c2 = ColorFromHex("#06b6d4"); }
            else if (pct < 80) { c1 = ColorFromHex("#facc15"); c2 = ColorFromHex("#eab308"); }
            else { c1 = ColorFromHex("#f87171"); c2 = ColorFromHex("#ef4444"); }

            return new LinearGradientBrush(c1, c2, 0);
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Color ColorFromHex(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}

public class ContextPercentToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct)
        {
            if (pct < 50) return new SolidColorBrush(ColorFromHex("#22d3ee"));
            if (pct < 80) return new SolidColorBrush(ColorFromHex("#facc15"));
            return new SolidColorBrush(ColorFromHex("#f87171"));
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Color ColorFromHex(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}

public class CostToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double cost)
            return new SolidColorBrush(ColorFromHex(cost > 1 ? "#facc15" : "#4ade80"));
        return new SolidColorBrush(ColorFromHex("#4ade80"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Color ColorFromHex(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}

public class RateLimitToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct && pct >= 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NegativeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct && pct < 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
