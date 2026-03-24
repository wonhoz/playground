using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - feDropShadow → feGaussianBlur+feOffset+feFlood+feComposite+feMerge 확장
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

    // <feDropShadow ... />
    static readonly Regex DropShadowRegex = new(
        @"<feDropShadow([^/]*?)/>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // <filter ...> — 명시적 x 속성이 없는 filter 태그 검색
    static readonly Regex FilterTagRegex = new(
        @"<filter\b([^>]*?)>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // 특정 속성 추출
    static readonly Regex AttrRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        // feDropShadow 확장 전에 필터 영역을 명시적으로 지정
        // (기본값 -10%/+120%는 폰트 메트릭에 따라 첫/끝 글자가 클리핑될 수 있음)
        if (DropShadowRegex.IsMatch(svgText))
            svgText = EnsureFilterRegion(svgText);
        svgText = ExpandDropShadows(svgText);
        return svgText;
    }

    // feDropShadow를 포함하는 SVG에서 <filter> 요소에 명시적 영역 속성 추가
    static string EnsureFilterRegion(string svg) =>
        FilterTagRegex.Replace(svg, m =>
        {
            string attrs = m.Groups[1].Value;
            // 이미 x 속성이 있으면 그대로 유지
            if (Regex.IsMatch(attrs, @"\bx\s*=")) return m.Value;
            return $"<filter{attrs} x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\">";
        });

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

    // ─── feDropShadow 확장 ──────────────────────────────────────────────────
    static string ExpandDropShadows(string svg) =>
        DropShadowRegex.Replace(svg, m =>
        {
            var attrs = ParseAttrs(m.Groups[1].Value);
            string dx  = attrs.GetValueOrDefault("dx",            "0");
            string dy  = attrs.GetValueOrDefault("dy",            "0");
            string std = attrs.GetValueOrDefault("stdDeviation",  "0");
            string fc  = attrs.GetValueOrDefault("flood-color",   "#000000");
            string fo  = attrs.GetValueOrDefault("flood-opacity", "1");

            return
                $"<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{std}\" result=\"ds_blur\"/>" +
                $"<feOffset dx=\"{dx}\" dy=\"{dy}\" result=\"ds_offset\"/>" +
                $"<feFlood flood-color=\"{fc}\" flood-opacity=\"{fo}\" result=\"ds_flood\"/>" +
                $"<feComposite in=\"ds_flood\" in2=\"ds_offset\" operator=\"in\" result=\"ds_shadow\"/>" +
                $"<feMerge>" +
                $"<feMergeNode in=\"ds_shadow\"/>" +
                $"<feMergeNode in=\"SourceGraphic\"/>" +
                $"</feMerge>";
        });

    static Dictionary<string, string> ParseAttrs(string attrText)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrRegex.Matches(attrText))
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        return dict;
    }
}
