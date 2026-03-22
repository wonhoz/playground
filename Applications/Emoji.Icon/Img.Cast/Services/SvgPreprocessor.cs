using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
/// - feDropShadow → feGaussianBlur+feOffset+feFlood+feComposite+feMerge 확장
/// </summary>
internal static class SvgPreprocessor
{
    // fill|stroke|flood-color|stop-color="#RRGGBBAA"
    static readonly Regex Hex8Regex = new(
        @"(fill|stroke|flood-color|stop-color)=""#([0-9A-Fa-f]{6})([0-9A-Fa-f]{2})""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // <feDropShadow ... />
    static readonly Regex DropShadowRegex = new(
        @"<feDropShadow([^/]*?)/>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // 특정 속성 추출
    static readonly Regex AttrRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        svgText = ConvertHex8Colors(svgText);
        svgText = ExpandDropShadows(svgText);
        return svgText;
    }

    // ─── 8자리 hex → 6자리 + opacity ────────────────────────────────────────
    static string ConvertHex8Colors(string svg) =>
        Hex8Regex.Replace(svg, m =>
        {
            string attr = m.Groups[1].Value;
            string rgb  = m.Groups[2].Value;
            int    a    = Convert.ToInt32(m.Groups[3].Value, 16);
            double op   = Math.Round(a / 255.0, 4);

            string opAttr = attr.ToLower() switch
            {
                "fill"        => "fill-opacity",
                "stroke"      => "stroke-opacity",
                "flood-color" => "flood-opacity",
                "stop-color"  => "stop-opacity",
                _             => attr + "-opacity",
            };
            return $"{attr}=\"#{rgb}\" {opAttr}=\"{op}\"";
        });

    // ─── feDropShadow 확장 ──────────────────────────────────────────────────
    static string ExpandDropShadows(string svg) =>
        DropShadowRegex.Replace(svg, m =>
        {
            var attrs = ParseAttrs(m.Groups[1].Value);
            string dx  = attrs.GetValueOrDefault("dx",              "0");
            string dy  = attrs.GetValueOrDefault("dy",              "0");
            string std = attrs.GetValueOrDefault("stdDeviation",    "0");
            string fc  = attrs.GetValueOrDefault("flood-color",     "#000000");
            string fo  = attrs.GetValueOrDefault("flood-opacity",   "1");

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
