using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Stock.Catch.Converters;

/// <summary>
/// 숫자의 부호에 따라 색을 반환(한국식: 양수=상승 빨강 / 음수=하락 파랑 / 0=보조색).
/// null·NaN은 Zero 색을 반환한다.
/// </summary>
public sealed class SignBrushConverter : IValueConverter
{
    public Brush Positive { get; set; } = Brushes.Red;
    public Brush Negative { get; set; } = Brushes.DodgerBlue;
    public Brush Zero { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value switch
        {
            decimal m => (double)m,
            double x => x,
            int i => i,
            long l => l,
            _ => double.NaN
        };
        if (double.IsNaN(d) || d == 0) return Zero;
        return d > 0 ? Positive : Negative;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
