using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - feDropShadow: Svg.Skia 3.x에서 네이티브 지원하므로 확장 불필요
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

    public static string Process(string svgText)
    {
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
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

    static string GetOpacityAttr(string attr) => attr.ToLower() switch
    {
        "fill"        => "fill-opacity",
        "stroke"      => "stroke-opacity",
        "flood-color" => "flood-opacity",
        "stop-color"  => "stop-opacity",
        _             => attr + "-opacity",
    };
}
