using System.Windows.Media;
using System.Windows;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LogLens.Controls;

/// <summary>
/// 키워드/정규식 하이라이트를 지원하는 경량 텍스트 렌더링 요소.
/// FrameworkElement를 상속해 FormattedText + DrawingContext로 직접 그린다.
/// VirtualizingStackPanel 안에서도 올바르게 작동한다.
/// </summary>
public class HighlightTextBlock : FrameworkElement
{
    // ── DependencyProperties ─────────────────────────────────────────
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata("",
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(Brushes.White,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightPatternProperty =
        DependencyProperty.Register(nameof(HighlightPattern), typeof(string), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata("",
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UseRegexProperty =
        DependencyProperty.Register(nameof(UseRegex), typeof(bool), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaseSensitiveProperty =
        DependencyProperty.Register(nameof(CaseSensitive), typeof(bool), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(new FontFamily("Consolas"),
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnTypefaceChanged));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(12.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnTypefaceChanged));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(HighlightTextBlock),
            new FrameworkPropertyMetadata(new Thickness(0),
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender));

    // ── 정적 하이라이트 브러시 (Frozen) ─────────────────────────────
    private static readonly SolidColorBrush _hlBg;
    private static readonly SolidColorBrush _hlFg;

    private Typeface _typeface = new("Consolas");

    static HighlightTextBlock()
    {
        _hlBg = new SolidColorBrush(Color.FromArgb(200, 255, 200, 0));
        _hlBg.Freeze();
        _hlFg = new SolidColorBrush(Colors.Black);
        _hlFg.Freeze();
    }

    private static void OnTypefaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HighlightTextBlock htb)
            htb._typeface = new Typeface(htb.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    }

    // ── Properties ──────────────────────────────────────────────────
    public string      Text             { get => (string)GetValue(TextProperty);             set => SetValue(TextProperty, value); }
    public Brush       Foreground       { get => (Brush)GetValue(ForegroundProperty);        set => SetValue(ForegroundProperty, value); }
    public string      HighlightPattern { get => (string)GetValue(HighlightPatternProperty); set => SetValue(HighlightPatternProperty, value); }
    public bool        UseRegex         { get => (bool)GetValue(UseRegexProperty);           set => SetValue(UseRegexProperty, value); }
    public bool        CaseSensitive    { get => (bool)GetValue(CaseSensitiveProperty);      set => SetValue(CaseSensitiveProperty, value); }
    public FontFamily  FontFamily       { get => (FontFamily)GetValue(FontFamilyProperty);   set => SetValue(FontFamilyProperty, value); }
    public double      FontSize         { get => (double)GetValue(FontSizeProperty);         set => SetValue(FontSizeProperty, value); }
    public Thickness   Padding          { get => (Thickness)GetValue(PaddingProperty);       set => SetValue(PaddingProperty, value); }

    // ── 레이아웃 ─────────────────────────────────────────────────────
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text ?? "";
        var pad  = Padding;
        var extraW = pad.Left + pad.Right;
        var extraH = pad.Top  + pad.Bottom;

        if (string.IsNullOrEmpty(text))
            return new Size(extraW, FontSize * 1.4 + extraH);

        var ft = MakeFt(text, Brushes.White, 1.0);
        return new Size(ft.Width + extraW, ft.Height + extraH);
    }

    // ── 렌더링 ───────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        var text = Text ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var fg  = Foreground ?? Brushes.LightGray;
        var dip = GetDip();
        var ft  = MakeFt(text, fg, dip);
        var origin = new Point(Padding.Left, Padding.Top);

        var pattern = HighlightPattern ?? "";
        if (!string.IsNullOrEmpty(pattern))
        {
            foreach (var (start, len) in GetMatchRanges(text, pattern, UseRegex, CaseSensitive))
            {
                if (len <= 0) continue;
                var geo = ft.BuildHighlightGeometry(origin, start, len);
                if (geo != null)
                    dc.DrawGeometry(_hlBg, null, geo);
                ft.SetForegroundBrush(_hlFg, start, len);
            }
        }

        dc.DrawText(ft, origin);
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────
    private FormattedText MakeFt(string text, Brush fg, double dip)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
               _typeface, FontSize, fg, dip);

    private double GetDip()
    {
        try
        {
            var src = PresentationSource.FromVisual(this);
            if (src?.CompositionTarget != null)
                return src.CompositionTarget.TransformToDevice.M11;
        }
        catch { }
        return 1.0;
    }

    private static IEnumerable<(int start, int len)> GetMatchRanges(
        string text, string pattern, bool useRegex, bool caseSensitive)
    {
        if (useRegex)
        {
            Regex rx;
            try
            {
                var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                rx = new Regex(pattern, opts);
            }
            catch { yield break; }

            foreach (Match m in rx.Matches(text))
                if (m.Length > 0) yield return (m.Index, m.Length);
        }
        else
        {
            var comp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var idx  = 0;
            while (idx < text.Length)
            {
                var pos = text.IndexOf(pattern, idx, comp);
                if (pos < 0) break;
                yield return (pos, pattern.Length);
                idx = pos + pattern.Length;
            }
        }
    }
}
