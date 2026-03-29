using System.Globalization;
using System.Text.RegularExpressions;

namespace ImgCast.Services;

/// <summary>
/// Svg.Skia 미지원 SVG 기능을 로드 전에 호환 형식으로 변환.
/// - CSS Color Level 4 8자리 hex (#RRGGBBAA) → 6자리 + *-opacity 분리
/// - feDropShadow → feGaussianBlur+feOffset+feFlood+feComposite+feMerge 확장
/// - dominant-baseline → dy 오프셋 보정
/// - &lt;defs&gt; 전방 참조 방지 — SVG 최상단으로 이동
/// - 필터 영역 → filterUnits="userSpaceOnUse" + 캔버스 전체 커버 (텍스트 앵커점 OBB 오계산 방지)
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

    // dominant-baseline
    static readonly Regex DominantBaselineCentralRegex = new(
        @"dominant-baseline=""central""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex DominantBaselineMiddleRegex = new(
        @"dominant-baseline=""middle""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex DominantBaselineHangingRegex = new(
        @"dominant-baseline=""hanging""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // <defs>...</defs> 블록 전체
    static readonly Regex DefsBlockRegex = new(
        @"<defs\b[^>]*>[\s\S]*?</defs>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // <defs>...</defs> 내부 캡처용
    static readonly Regex DefsContentRegex = new(
        @"<defs\b[^>]*>([\s\S]*?)</defs>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // <svg ...> 오프닝 태그
    static readonly Regex SvgOpenTagRegex = new(
        @"<svg\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // <filter ...> 오프닝 태그
    static readonly Regex FilterTagRegex = new(
        @"<filter\b([^>]*?)>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // filter 태그 내 영역/단위 속성 (제거 대상)
    static readonly Regex FilterRegionAttrRegex = new(
        @"\s*(x|y|width|height|filterUnits)\s*=\s*""[^""]*""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // viewBox 파싱
    static readonly Regex ViewBoxRegex = new(
        @"viewBox\s*=\s*""[^\s""]+\s+[^\s""]+\s+([0-9.]+)\s+([0-9.]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 속성값 추출용
    static readonly Regex AttrRegex = new(
        @"(\w[\w-]*)=""([^""]*)""",
        RegexOptions.Compiled);

    public static string Process(string svgText)
    {
        var (vbW, vbH) = ParseViewBox(svgText);
        svgText = EnsureDefsFirst(svgText);
        svgText = EnsureFiltersUserSpaceOnUse(svgText, vbW, vbH);
        svgText = ConvertHex8AttrColors(svgText);
        svgText = ConvertHex8CssColors(svgText);
        svgText = ExpandDropShadows(svgText);
        svgText = FixDominantBaseline(svgText);
        return svgText;
    }

    // ─── <defs> 전방 참조 방지 — SVG 최상단으로 이동 ──────────────────────────
    // locale.view.2 등: fill="url(#bg1)" 사용 후 <defs>에 bg1 정의 → Svg.Skia 렌더링 실패
    // 모든 <defs> 내용을 하나로 합쳐 <svg> 오프닝 태그 바로 뒤에 삽입.
    static string EnsureDefsFirst(string svg)
    {
        var contents = new List<string>();
        DefsContentRegex.Replace(svg, m => { contents.Add(m.Groups[1].Value); return ""; });
        if (contents.Count == 0) return svg;

        string withoutDefs = DefsBlockRegex.Replace(svg, "");
        string merged = $"<defs>{string.Concat(contents)}</defs>";
        return SvgOpenTagRegex.Replace(withoutDefs, m => m.Value + merged, 1);
    }

    // ─── 필터 영역 → filterUnits="userSpaceOnUse" + 캔버스 전체 커버 ────────────
    // text-anchor="middle" 텍스트에 필터 적용 시 Svg.Skia가 objectBoundingBox를
    // 앵커점 기준 0폭으로 계산 → 필터 출력 영역이 0 → 콘텐츠 클리핑/누락.
    // userSpaceOnUse + 캔버스 전체 + 여유 margin으로 확실히 커버.
    static string EnsureFiltersUserSpaceOnUse(string svg, float vbW, float vbH)
    {
        if (vbW <= 0 || vbH <= 0) return svg;
        const float margin = 200f;
        string x = (-margin).ToString("F0", CultureInfo.InvariantCulture);
        string y = (-margin).ToString("F0", CultureInfo.InvariantCulture);
        string w = (vbW + 2 * margin).ToString("F0", CultureInfo.InvariantCulture);
        string h = (vbH + 2 * margin).ToString("F0", CultureInfo.InvariantCulture);

        return FilterTagRegex.Replace(svg, m =>
        {
            // 기존 영역/단위 속성 제거 후 userSpaceOnUse + 전체 캔버스 커버 적용
            string cleaned = FilterRegionAttrRegex.Replace(m.Groups[1].Value, "");
            return $"<filter{cleaned} filterUnits=\"userSpaceOnUse\" x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\">";
        });
    }

    // ─── feDropShadow → SVG 1.1 동등 필터 프리미티브 ──────────────────────────
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

    // ─── CSS 형식: fill:#RRGGBBAA → fill:#RRGGBB; fill-opacity:X ─────────────
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
    static string FixDominantBaseline(string svg)
    {
        svg = DominantBaselineCentralRegex.Replace(svg, @"dy=""0.35em""");
        svg = DominantBaselineMiddleRegex .Replace(svg, @"dy=""0.35em""");
        svg = DominantBaselineHangingRegex.Replace(svg, @"dy=""0.72em""");
        return svg;
    }

    static (float W, float H) ParseViewBox(string svg)
    {
        var m = ViewBoxRegex.Match(svg);
        if (m.Success
            && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float w)
            && float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            return (w, h);
        return (0, 0);
    }

    static Dictionary<string, string> ParseAttrs(string attrText)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrRegex.Matches(attrText))
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        return dict;
    }
}
