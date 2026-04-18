using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Prompt.Forge.Controls;

public static class TagColorHelper
{
    static readonly string[] Palette =
    [
        "#4FC3F7", // sky
        "#81C784", // green
        "#FFB74D", // orange
        "#F48FB1", // pink
        "#CE93D8", // purple
        "#80DEEA", // teal
        "#FFF176", // yellow
        "#FF8A65", // salmon
        "#A5D6A7", // mint
        "#90CAF9", // blue
    ];

    public static string GetHex(string tag)
    {
        var h = Math.Abs(tag.GetHashCode()) % Palette.Length;
        return Palette[h];
    }
}

[ValueConversion(typeof(string), typeof(Brush))]
public sealed class TagColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = TagColorHelper.GetHex(value as string ?? "");
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
