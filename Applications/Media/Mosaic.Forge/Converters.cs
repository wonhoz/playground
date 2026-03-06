using System.Globalization;
using System.Windows.Data;

namespace Mosaic.Forge;

[ValueConversion(typeof(bool), typeof(Visibility))]
sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(bool))]
sealed class BoolNegateConverter : IValueConverter
{
    public static readonly BoolNegateConverter Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is bool b && !b;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => v is bool b && !b;
}
