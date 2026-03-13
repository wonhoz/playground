using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace PadForge.Views;

/// <summary>bool → SolidColorBrush 변환기</summary>
public class BoolToBrushConverter : IValueConverter
{
    public string TrueBrush  { get; set; } = "#4CAF50";
    public string FalseBrush { get; set; } = "#888888";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        var hex = b ? TrueBrush : FalseBrush;
        return (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => WpfBinding.DoNothing;
}

/// <summary>Count == 0 → Visible, 그 외 → Collapsed</summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public class ZeroToVisConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is int i && i == 0) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => WpfBinding.DoNothing;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

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

/// <summary>double (0~100) → Width/Height 으로 Canvas 내 위치 계산</summary>
public class StickPosConverter : IValueConverter
{
    public static readonly StickPosConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double v = value is double d ? d : 50;
        // 0~100 범위를 Canvas 100px 기준으로 offset 계산 (thumb는 10px 크기)
        return Math.Clamp(v - 5, 0, 90);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => WpfBinding.DoNothing;
}
