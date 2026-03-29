using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
///   (attribute 형식 및 CSS style="" 내부 형식 모두 처리)
/// - feDropShadow → feGaussianBlur+feOffset+feFlood+feComposite+feMerge 확장
///   (필터 내 result ID는 고정명 사용 — SVG 스펙상 filter 요소별로 스코프됨)
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

    // <feDropShadow ... />
    static readonly Regex DropShadowRegex = new(
        @"<feDropShadow([^/]*?)/>",
        RegexOptions.Compiled | RegexOptions.Singleline);

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

    // 속성값 추출용
    static readonly Regex AttrRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = ExpandDropShadows(svgText);
        svgText = FixDominantBaseline(svgText);
        return svgText;
    }

    // ─── feDropShadow → SVG 1.1 동등 필터 프리미티브 ──────────────────────────
    // result ID는 고정명(ds_blur 등) 사용. SVG 스펙상 filter 요소 내부로 스코프되므로
    // 서로 다른 filter 요소에서 동일 ID를 사용해도 충돌 없음.
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

    // ─── dominant-baseline → dy 보정 ────────────────────────────────────────
    // Svg.Skia가 dominant-baseline을 지원하지 않아 텍스트 수직 위치가 틀어지는 문제 해결.
    // text-anchor="middle"과 함께 쓰이는 경우를 대상으로 dy="0.35em" 오프셋 추가.
    static string FixDominantBaseline(string svg)
    {
        // central / middle → 중앙 정렬 (em 기준 0.35 오프셋이 시각적으로 가장 근사)
        svg = DominantBaselineCentralRegex.Replace(svg, @"dy=""0.35em""");
        svg = DominantBaselineMiddleRegex .Replace(svg, @"dy=""0.35em""");
        // hanging → 상단 정렬 (-0.7em 근사)
        svg = DominantBaselineHangingRegex.Replace(svg, @"dy=""-0.7em""");
        return svg;
    }

    static Dictionary<string, string> ParseAttrs(string attrText)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrRegex.Matches(attrText))
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        return dict;
    }
}
