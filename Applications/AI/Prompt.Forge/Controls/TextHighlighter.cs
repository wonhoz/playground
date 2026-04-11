using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Prompt.Forge.Controls;

/// 검색 키워드 하이라이팅 Attached Property
public static class TextHighlighter
{
    public static readonly DependencyProperty RawTextProperty =
        DependencyProperty.RegisterAttached("RawText", typeof(string), typeof(TextHighlighter),
            new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.RegisterAttached("Query", typeof(string), typeof(TextHighlighter),
            new PropertyMetadata("", OnChanged));

    public static string GetRawText(DependencyObject obj) => (string)obj.GetValue(RawTextProperty);
    public static void SetRawText(DependencyObject obj, string value) => obj.SetValue(RawTextProperty, value);
    public static string GetQuery(DependencyObject obj) => (string)obj.GetValue(QueryProperty);
    public static void SetQuery(DependencyObject obj, string value) => obj.SetValue(QueryProperty, value);

    static readonly Brush HighlightBg = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
    static readonly Brush HighlightFg = Brushes.White;

    static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        UpdateInlines(tb, GetRawText(tb) ?? "", GetQuery(tb) ?? "");
    }

    static void UpdateInlines(TextBlock tb, string text, string query)
    {
        tb.Inlines.Clear();
        if (string.IsNullOrEmpty(text)) return;

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        // 각 토큰을 순차적으로 하이라이팅
        var segments = new List<(int start, int end)>();
        foreach (var token in tokens)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                int idx = text.IndexOf(token, pos, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                segments.Add((idx, idx + token.Length));
                pos = idx + token.Length;
            }
        }

        if (segments.Count == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        // 겹치는 세그먼트 병합 후 정렬
        segments.Sort((a, b) => a.start.CompareTo(b.start));
        var merged = new List<(int start, int end)>();
        foreach (var seg in segments)
        {
            if (merged.Count > 0 && seg.start <= merged[^1].end)
                merged[^1] = (merged[^1].start, Math.Max(merged[^1].end, seg.end));
            else
                merged.Add(seg);
        }

        int cur = 0;
        foreach (var (start, end) in merged)
        {
            if (start > cur) tb.Inlines.Add(new Run(text[cur..start]));
            tb.Inlines.Add(new Run(text[start..end])
            {
                Background = HighlightBg,
                Foreground = HighlightFg
            });
            cur = end;
        }
        if (cur < text.Length) tb.Inlines.Add(new Run(text[cur..]));
    }
}
