using System.Text.RegularExpressions;
using System.Windows.Media;

namespace LogLens.Models;

public sealed class HighlightRule
{
    public string Pattern { get; set; } = "";
    public Brush Foreground { get; set; } = Brushes.White;
    public bool IsRegex { get; set; }

    private Regex? _compiled;

    public bool IsMatch(string text)
    {
        if (string.IsNullOrEmpty(Pattern)) return false;

        if (IsRegex)
        {
            _compiled ??= new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return _compiled.IsMatch(text);
        }

        return text.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
    }
}
