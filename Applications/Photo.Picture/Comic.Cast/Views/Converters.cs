using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ComicCast.Views;

/// <summary>bool → Visibility</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisConverter : MarkupExtension, IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => WpfBinding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>double → string 포맷 (예: 배율)</summary>
public class DoubleFormatConverter : IValueConverter
{
    public string Format { get; set; } = "{0:P0}";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Format(Format, value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => WpfBinding.DoNothing;
}

/// <summary>double 0~1 → ProgressBar Width (부모 기준)</summary>
public class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double pct && values[1] is double totalW)
            return totalW * (pct / 100.0);
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => [WpfBinding.DoNothing];
}
