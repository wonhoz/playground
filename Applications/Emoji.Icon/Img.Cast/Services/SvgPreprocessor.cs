using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - dominant-baseline → dy 오프셋 보정 (Svg.Skia 미지원값)
/// - feDropShadow: flood-color + flood-opacity → rgba() 통합
///   (Svg.Skia 3.x는 feDropShadow 네이티브 지원, 단 별도 flood-opacity는 미처리)
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

    static readonly Regex FloodOpacityAttrRegex = new(
        @"\s*flood-opacity=""([^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 속성값 추출용
    static readonly Regex AttrValueRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        svgText = MergeFeDropShadowOpacity(svgText);
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = FixDominantBaseline(svgText);
        return svgText;
    }

    // ─── feDropShadow: flood-color + flood-opacity → rgba() 통합 ─────────────
    // Svg.Skia 3.x는 feDropShadow를 네이티브 지원하지만 별도 flood-opacity 속성을
    // 처리하지 못해 그림자 색이 잘못 적용됨. flood-color="rgba(r,g,b,a)" 형식으로 통합.
    static string MergeFeDropShadowOpacity(string svg) =>
        FeDropShadowRegex.Replace(svg, m =>
        {
            var attrStr = m.Groups[1].Value;
            var attrs   = ParseAttrs(attrStr);

            if (!attrs.TryGetValue("flood-color",   out string? fc) ||
                !attrs.TryGetValue("flood-opacity",  out string? fo))
                return m.Value; // 별도 flood-opacity 없으면 변환 불필요

            string? rgba = TryHexToRgba(fc, fo);
            if (rgba == null) return m.Value;

            // flood-color 값 교체 후 flood-opacity 속성 제거
            string newAttrs = Regex.Replace(attrStr,
                @"flood-color=""[^""]*""", $@"flood-color=""{rgba}""",
                RegexOptions.IgnoreCase);
            newAttrs = FloodOpacityAttrRegex.Replace(newAttrs, "");

            return $"<feDropShadow{newAttrs}/>";
        });

    static string? TryHexToRgba(string colorHex, string opacityStr)
    {
        if (!float.TryParse(opacityStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float opacity))
            return null;

        colorHex = colorHex.TrimStart('#');
        int r, g, b;
        if (colorHex.Length == 6)
        {
            r = Convert.ToInt32(colorHex[0..2], 16);
            g = Convert.ToInt32(colorHex[2..4], 16);
            b = Convert.ToInt32(colorHex[4..6], 16);
        }
        else if (colorHex.Length == 3)
        {
            r = Convert.ToInt32(new string(colorHex[0], 2), 16);
            g = Convert.ToInt32(new string(colorHex[1], 2), 16);
            b = Convert.ToInt32(new string(colorHex[2], 2), 16);
        }
        else return null;

        string opStr = opacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"rgba({r},{g},{b},{opStr})";
    }

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
