using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - dominant-baseline → dy 오프셋 보정 (Svg.Skia 미지원값)
/// - feDropShadow → SVG 1.1 동등 필터 프리미티브로 변환
///   (Skia 엔진이 feDropShadow 단축형을 미지원하여 필터 그룹 전체가 렌더링 안 되는 문제 해결)
/// </summary>
internal static class SvgPreprocessor
{
    // fill|stroke|..="#RRGGBBAA" (attribute 형식)
    static readonly Regex Hex8AttrRegex = new(
        @"(fill|stroke|flood-color|stop-color)=""#([0-9A-Fa-f]{6})([0-9A-Fa-f]{2})""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // fill|stroke|..:#RRGGBBAA (CSS style="" 내부 형식)
    static readonly Regex Hex8CssRegex = new(
        @"(fill|stroke|flood-color|stop-color)\s*:\s*#([0-9A-Fa-f]{6})([0-9A-Fa-f]{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // dominant-baseline → Svg.Skia 미지원값, dy로 보정
    static readonly Regex DominantBaselineCentralRegex = new(
        @"dominant-baseline=""central""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex DominantBaselineMiddleRegex = new(
        @"dominant-baseline=""middle""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex DominantBaselineHangingRegex = new(
        @"dominant-baseline=""hanging""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // feDropShadow 자기닫힘 태그 (SVG Filter Effects Level 2 단축형)
    static readonly Regex FeDropShadowRegex = new(
        @"<feDropShadow([^>]*?)/>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // 속성값 추출용
    static readonly Regex AttrValueRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    static int _shadowCounter = 0;

    public static string Process(string svgText)
    {
        svgText = ExpandFeDropShadow(svgText);
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = FixDominantBaseline(svgText);
        return svgText;
    }

    // ─── feDropShadow → SVG 1.1 동등 필터 프리미티브 ──────────────────────────
    // Skia 엔진은 feDropShadow(SVG FE Level 2 단축형)를 미지원.
    // feGaussianBlur + feOffset + feFlood + feComposite + feMerge 조합으로 전개.
    static string ExpandFeDropShadow(string svg) =>
        FeDropShadowRegex.Replace(svg, m =>
        {
            var attrs = ParseAttrs(m.Groups[1].Value);
            string dx = attrs.GetValueOrDefault("dx", "2");
            string dy = attrs.GetValueOrDefault("dy", "2");
            string sd = attrs.GetValueOrDefault("stdDeviation", "4");
            string fc = attrs.GetValueOrDefault("flood-color", "black");
            string fo = attrs.GetValueOrDefault("flood-opacity", "1");

            int n = System.Threading.Interlocked.Increment(ref _shadowCounter);
            return
                $"""<feGaussianBlur in="SourceAlpha" stdDeviation="{sd}" result="blur{n}"/>""" +
                $"""<feOffset in="blur{n}" dx="{dx}" dy="{dy}" result="offset{n}"/>""" +
                $"""<feFlood flood-color="{fc}" flood-opacity="{fo}" result="flood{n}"/>""" +
                $"""<feComposite in="flood{n}" in2="offset{n}" operator="in" result="shadow{n}"/>""" +
                $"""<feMerge><feMergeNode in="shadow{n}"/><feMergeNode in="SourceGraphic"/></feMerge>""";
        });

    static Dictionary<string, string> ParseAttrs(string attrString)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrValueRegex.Matches(attrString))
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        return dict;
    }

    // ─── attribute 형식: fill="#RRGGBBAA" → fill="#RRGGBB" fill-opacity="X" ──
    static string ConvertHex8AttrColors(string svg) =>
        Hex8AttrRegex.Replace(svg, m =>
        {
            string attr = m.Groups[1].Value;
            string rgb  = m.Groups[2].Value;
            int    a    = Convert.ToInt32(m.Groups[3].Value, 16);
            double op   = Math.Round(a / 255.0, 4);
            return $"{attr}=\"#{rgb}\" {GetOpacityAttr(attr)}=\"{op}\"";
        });

    // ─── CSS 형식: fill:#RRGGBBAA → fill:#RRGGBB; fill-opacity:X ─────────
    static string ConvertHex8CssColors(string svg) =>
        Hex8CssRegex.Replace(svg, m =>
        {
            string attr = m.Groups[1].Value;
            string rgb  = m.Groups[2].Value;
            int    a    = Convert.ToInt32(m.Groups[3].Value, 16);
            double op   = Math.Round(a / 255.0, 4);
            return $"{attr}:#{rgb}; {GetOpacityAttr(attr)}:{op}";
        });

    // ─── dominant-baseline → dy 보정 ─────────────────────────────────────────
    // Svg.Skia가 dominant-baseline을 무시해 글자 위치가 어긋나는 문제 해결.
    // "central"/"middle" → dy="0.35em": 알파벳 베이스라인 → cap 시각 중심 오프셋 보정
    // "hanging"          → dy="0.72em": 텍스트가 y에서 아래로 걸려야 하는데
    //                     미지원 시 베이스라인이 y에 오므로 cap_top(≈0.72em) 만큼 아래로 밀어야 함
    static string FixDominantBaseline(string svg)
    {
        svg = DominantBaselineCentralRegex.Replace(svg, @"dy=""0.35em""");
        svg = DominantBaselineMiddleRegex.Replace(svg, @"dy=""0.35em""");
        svg = DominantBaselineHangingRegex.Replace(svg, @"dy=""0.72em""");
        return svg;
    }

    static string GetOpacityAttr(string attr) => attr.ToLower() switch
    {
        "fill"        => "fill-opacity",
        "stroke"      => "stroke-opacity",
        "flood-color" => "flood-opacity",
        "stop-color"  => "stop-opacity",
        _             => attr + "-opacity",
    };
}
