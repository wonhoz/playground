using System.Globalization;
using System.Windows.Data;

namespace StockRush.Controls;

public class NonEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class Converters
{
    public static IValueConverter NonEmptyToVisible { get; } = new NonEmptyToVisibleConverter();
}
