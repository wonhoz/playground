using System.Globalization;
using System.Windows.Data;

namespace PadForge.Views;

/// <summary>IsKey OR IsText → Collapsed (둘 다 false면 Visible)</summary>
public class NorVisConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool any = values.OfType<bool>().Any(b => b);
        return any ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
