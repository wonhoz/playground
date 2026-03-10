using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LocaleForge;

// x:Static으로 접근할 수 있도록 최상위 네임스페이스에 배치
public class BoolToBgConverter : IValueConverter
{
    public static readonly BoolToBgConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)) : Brushes.Transparent;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToVisConverter : IValueConverter
{
    public static readonly BoolToVisConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class FileNameConverter : IValueConverter
{
    public static readonly FileNameConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string path ? Path.GetFileName(path) : string.Empty;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
