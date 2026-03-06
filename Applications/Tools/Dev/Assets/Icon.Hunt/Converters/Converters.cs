using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace IconHunt;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true
            ? new SolidColorBrush(Color.FromRgb(0xE9, 0x45, 0x60))
            : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToStarConverter : IValueConverter
{
    public static readonly BoolToStarConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? "★" : "☆";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToVisConverter : IValueConverter
{
    public static readonly BoolToVisConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class InverseBoolToVisConverter : IValueConverter
{
    public static readonly InverseBoolToVisConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class NullToVisConverter : IValueConverter
{
    public static readonly NullToVisConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value?.ToString() ?? "#1A1A2E")); }
        catch { return new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)); }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class PrefixBadgeColorConverter : IValueConverter
{
    public static readonly PrefixBadgeColorConverter Instance = new();
    private static readonly Dictionary<string, SolidColorBrush> _colors = new()
    {
        ["mdi"]              = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
        ["material-symbols"] = new SolidColorBrush(Color.FromRgb(0x03, 0xA9, 0xF4)),
        ["heroicons"]        = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x99)),
        ["ph"]               = new SolidColorBrush(Color.FromRgb(0xFB, 0x7B, 0x55)),
        ["lucide"]           = new SolidColorBrush(Color.FromRgb(0xF5, 0x60, 0x40)),
        ["tabler"]           = new SolidColorBrush(Color.FromRgb(0x22, 0x6D, 0xB5)),
        ["bi"]               = new SolidColorBrush(Color.FromRgb(0x7B, 0x2F, 0xBF)),
        ["feather"]          = new SolidColorBrush(Color.FromRgb(0x3A, 0xBF, 0x8E)),
        ["ri"]               = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x00)),
        ["carbon"]           = new SolidColorBrush(Color.FromRgb(0x16, 0x1C, 0x1F)),
        ["ic"]               = new SolidColorBrush(Color.FromRgb(0xEA, 0x43, 0x35)),
        ["fa-solid"]         = new SolidColorBrush(Color.FromRgb(0x52, 0x8B, 0xFF)),
        ["fa-regular"]       = new SolidColorBrush(Color.FromRgb(0x52, 0x8B, 0xFF)),
        ["la"]               = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x44)),
        ["ion"]              = new SolidColorBrush(Color.FromRgb(0x47, 0x6C, 0xFF)),
        ["octicon"]          = new SolidColorBrush(Color.FromRgb(0x24, 0x29, 0x2E)),
    };
    private static readonly SolidColorBrush _default = new(Color.FromRgb(0x44, 0x44, 0x66));

    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is string prefix && _colors.TryGetValue(prefix, out var brush) ? brush : _default;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
