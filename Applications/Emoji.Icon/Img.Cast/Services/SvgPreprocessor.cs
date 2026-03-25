using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - feDropShadow → 기본 필터 프리미티브 확장
///   (Svg.Skia가 flood-opacity 분리 후 feDropShadow를 처리하지 못해
///    filter 참조 요소 전체가 렌더링 스킵되는 문제 해결)
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

    // <feDropShadow ... /> 또는 <feDropShadow ...></feDropShadow>
    static readonly Regex FeDropShadowRegex = new(
        @"<feDropShadow\s+([^>]*?)(?:/>|>\s*</feDropShadow>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // attribute="value" 파싱용
    static readonly Regex AttrValueRegex = new(
        @"([\w-]+)\s*=\s*""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = ExpandFeDropShadow(svgText);
        return svgText;
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

    // ─── feDropShadow → 기본 필터 프리미티브 확장 ──────────────────────────
    // feGaussianBlur + feOffset + feFlood + feComposite + feMerge 조합으로 변환
    static string ExpandFeDropShadow(string svg) =>
        FeDropShadowRegex.Replace(svg, m =>
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match am in AttrValueRegex.Matches(m.Groups[1].Value))
                attrs[am.Groups[1].Value] = am.Groups[2].Value;

            string dx           = attrs.GetValueOrDefault("dx", "0");
            string dy           = attrs.GetValueOrDefault("dy", "0");
            string stdDev       = attrs.GetValueOrDefault("stdDeviation", "2");
            string floodColor   = attrs.GetValueOrDefault("flood-color", "#000000");
            string floodOpacity = attrs.GetValueOrDefault("flood-opacity", "1");

            return
                $"<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{stdDev}\" result=\"blur\"/>" +
                $"<feOffset in=\"blur\" dx=\"{dx}\" dy=\"{dy}\" result=\"offsetBlur\"/>" +
                $"<feFlood flood-color=\"{floodColor}\" flood-opacity=\"{floodOpacity}\" result=\"shadowColor\"/>" +
                $"<feComposite in=\"shadowColor\" in2=\"offsetBlur\" operator=\"in\" result=\"shadow\"/>" +
                $"<feMerge><feMergeNode in=\"shadow\"/><feMergeNode in=\"SourceGraphic\"/></feMerge>";
        });

    static string GetOpacityAttr(string attr) => attr.ToLower() switch
    {
        "fill"        => "fill-opacity",
        "stroke"      => "stroke-opacity",
        "flood-color" => "flood-opacity",
        "stop-color"  => "stop-opacity",
        _             => attr + "-opacity",
    };
}
