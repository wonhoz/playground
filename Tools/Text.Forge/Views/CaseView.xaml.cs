using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace TextForge.Views;

public partial class CaseView : UserControl
{
    public CaseView() => InitializeComponent();

    private void Upper_Click(object sender, RoutedEventArgs e)  => Convert(s => s.ToUpper(), "UPPER CASE");
    private void Lower_Click(object sender, RoutedEventArgs e)  => Convert(s => s.ToLower(), "lower case");
    private void Title_Click(object sender, RoutedEventArgs e)  => Convert(ToTitleCase,      "Title Case");
    private void Camel_Click(object sender, RoutedEventArgs e)  => Convert(ToCamelCase,      "camelCase");
    private void Snake_Click(object sender, RoutedEventArgs e)  => Convert(ToSnakeCase,      "snake_case");
    private void Pascal_Click(object sender, RoutedEventArgs e) => Convert(ToPascalCase,     "PascalCase");
    private void Kebab_Click(object sender, RoutedEventArgs e)  => Convert(ToKebabCase,      "kebab-case");

    private void Convert(Func<string, string> converter, string label)
    {
        var input = InputBox.Text;
        if (string.IsNullOrEmpty(input)) return;

        // 여러 줄이면 각 줄마다 변환
        var lines = input.Split('\n');
        OutputBox.Text = string.Join("\n", lines.Select(l => converter(l.TrimEnd('\r'))));
        ShowStatus($"✓ {label} 변환 완료", "#80DEEA");
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
        => (InputBox.Text, OutputBox.Text) = (OutputBox.Text, InputBox.Text);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputBox.Text))
        {
            Clipboard.SetText(OutputBox.Text);
            ShowStatus("✓ 클립보드에 복사됨", "#81C784");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text   = string.Empty;
        OutputBox.Text  = string.Empty;
        StatusText.Text = string.Empty;
    }

    // ── 변환 로직 ─────────────────────────────────────────────

    private static string ToTitleCase(string s)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());

    // camelCase / PascalCase / snake_case / kebab-case 공통 기반: 단어 분리
    private static string[] SplitWords(string s)
    {
        // 이미 snake_case / kebab-case 형태인 경우
        if (s.Contains('_') || s.Contains('-'))
            return s.Split('_', '-', ' ').Where(w => !string.IsNullOrEmpty(w)).ToArray();

        // camelCase / PascalCase → 단어 분리
        var result = Regex.Replace(s, @"([a-z])([A-Z])", "$1 $2");
        result = Regex.Replace(result, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
        return result.Split(' ').Where(w => !string.IsNullOrEmpty(w)).ToArray();
    }

    private static string ToCamelCase(string s)
    {
        var words = SplitWords(s);
        if (words.Length == 0) return s;
        var sb = new StringBuilder(words[0].ToLower());
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length == 0) continue;
            sb.Append(char.ToUpper(words[i][0]));
            sb.Append(words[i][1..].ToLower());
        }
        return sb.ToString();
    }

    private static string ToPascalCase(string s)
    {
        var words = SplitWords(s);
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (w.Length == 0) continue;
            sb.Append(char.ToUpper(w[0]));
            sb.Append(w[1..].ToLower());
        }
        return sb.ToString();
    }

    private static string ToSnakeCase(string s)
        => string.Join("_", SplitWords(s).Select(w => w.ToLower()));

    private static string ToKebabCase(string s)
        => string.Join("-", SplitWords(s).Select(w => w.ToLower()));

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
