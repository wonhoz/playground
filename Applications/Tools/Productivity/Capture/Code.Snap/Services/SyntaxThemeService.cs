using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeSnap.Services;

public static class SyntaxThemeService
{
    // 테마별 색상 팔레트
    private static readonly Dictionary<CodeTheme, ThemePalette> Palettes = new()
    {
        [CodeTheme.Dracula]   = new("#282A36", "#F8F8F2", "#FF79C6", "#F1FA8C", "#6272A4", "#BD93F9"),
        [CodeTheme.DarkPlus]  = new("#1E1E1E", "#D4D4D4", "#569CD6", "#CE9178", "#6A9955", "#B5CEA8"),
        [CodeTheme.GitHub]    = new("#FFFFFF", "#24292E", "#D73A49", "#032F62", "#6A737D", "#005CC5"),
        [CodeTheme.Nord]      = new("#2E3440", "#D8DEE9", "#81A1C1", "#A3BE8C", "#4C566A", "#B48EAD"),
        [CodeTheme.Monokai]   = new("#272822", "#F8F8F2", "#F92672", "#E6DB74", "#75715E", "#AE81FF"),
        [CodeTheme.Solarized] = new("#002B36", "#839496", "#268BD2", "#2AA198", "#586E75", "#D33682"),
    };

    // AvalonEdit 내장 언어명 매핑
    private static readonly Dictionary<string, string> LangMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"]         = "C#",
        ["Python"]     = "Python",
        ["JavaScript"] = "JavaScript",
        ["TypeScript"] = "TypeScript",
        ["Java"]       = "Java",
        ["HTML"]       = "HTML",
        ["CSS"]        = "CSS",
        ["SQL"]        = "SQL",
        ["XML"]        = "XML",
        ["JSON"]       = "Json",
        ["Markdown"]   = "MarkDown",
        ["PHP"]        = "PHP",
        ["Ruby"]       = "Ruby",
        ["Go"]         = "C#",      // Go 없음 → C# fallback
        ["Rust"]       = "C#",      // Rust 없음 → C# fallback
        ["Text"]       = null!,
    };

    public static void Apply(TextEditor editor, string language, CodeTheme theme)
    {
        var palette = Palettes[theme];

        editor.Background = BrushOf(palette.Bg);
        editor.Foreground = BrushOf(palette.Fg);
        editor.LineNumbersForeground = BrushOf(palette.Comment);

        if (!LangMap.TryGetValue(language, out var avLang) || avLang == null)
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        var baseDef = HighlightingManager.Instance.GetDefinition(avLang);
        if (baseDef == null)
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        var overrides = BuildOverrides(palette);
        editor.SyntaxHighlighting = new ThemeAwareHighlightingDefinition(baseDef, overrides);
    }

    private static Dictionary<string, HighlightingColor> BuildOverrides(ThemePalette p)
    {
        var kw  = ColorOf(p.Keyword);
        var str = ColorOf(p.Str);
        var cmt = ColorOf(p.Comment);
        var num = ColorOf(p.Number);
        var fg  = ColorOf(p.Fg);

        return new Dictionary<string, HighlightingColor>
        {
            ["Comment"]              = cmt,
            ["String"]               = str,
            ["Char"]                 = str,
            ["Keyword"]              = kw,
            ["Preprocessor"]         = kw,
            ["MethodCall"]           = fg,
            ["NumberLiteral"]        = num,
            ["ThisOrBaseReference"]  = kw,
            ["TypeReference"]        = ColorOf(p.Number),
            ["NamespaceOrTypeName"]  = fg,
            ["Digits"]               = num,
            ["Type keywords"]        = kw,
            ["Keywords"]             = kw,
            ["Operator"]             = fg,
        };
    }

    private static SolidColorBrush BrushOf(string hex) =>
        new(ColorFromHex(hex));

    private static HighlightingColor ColorOf(string hex) =>
        new() { Foreground = new SimpleHighlightingBrush(ColorFromHex(hex)) };

    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return System.Windows.Media.Color.FromRgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private record ThemePalette(string Bg, string Fg, string Keyword, string Str, string Comment, string Number);
}
