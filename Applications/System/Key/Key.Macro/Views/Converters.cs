using System.Globalization;
using System.Windows.Data;

namespace KeyMacro.Views;

/// <summary>0부터 시작하는 인덱스를 1부터 시작하는 순번으로 표시</summary>
public sealed class OrdinalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? (i + 1).ToString() : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>열거형 값과 ComboBox 선택 인덱스를 연결</summary>
public sealed class EnumIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? 0 : System.Convert.ToInt32(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && targetType.IsEnum ? Enum.ToObject(targetType, i) : Binding.DoNothing;
}
