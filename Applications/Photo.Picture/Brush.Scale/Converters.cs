using System.Globalization;
using System.Windows.Data;

namespace Brush.Scale;

public class BoolToVisibility : IValueConverter
{
    public static readonly BoolToVisibility Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

public class BoolToCollapsed : IValueConverter
{
    public static readonly BoolToCollapsed Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
