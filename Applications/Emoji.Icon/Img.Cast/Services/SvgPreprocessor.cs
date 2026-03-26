using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - dominant-baseline → dy 오프셋 보정 (Svg.Skia 미지원값)
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

    static readonly Regex DominantBaselineHangingRegex = new(
        @"dominant-baseline=""hanging""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Process(string svgText)
    {
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = FixDominantBaseline(svgText);
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

    // ─── dominant-baseline → dy 보정 ─────────────────────────────────────────
    // Svg.Skia가 dominant-baseline을 무시해 글자 위치가 어긋나는 문제 해결.
    // "central"  → dy="0.35em": 알파벳 베이스라인 → cap 시각 중심 오프셋 보정
    // "hanging"  → dy="0.72em": 텍스트가 y에서 아래로 걸려야 하는데
    //              미지원 시 베이스라인이 y에 오므로 cap_top(≈0.72em) 만큼 아래로 밀어야 함
    static string FixDominantBaseline(string svg)
    {
        svg = DominantBaselineCentralRegex.Replace(svg, @"dy=""0.35em""");
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
