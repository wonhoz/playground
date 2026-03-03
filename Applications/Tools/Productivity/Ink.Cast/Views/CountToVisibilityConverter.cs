using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InkCast.Views;

/// <summary>Count > 0 이면 Visible, 아니면 Collapsed</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public static readonly CountToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
