using System.Text;

namespace BadgeForge.Services;

public enum BadgeStyle { Flat, FlatSquare, ForTheBadge, Plastic, Social }

public record BadgeConfig(
    string Label,
    string Value,
    string LabelColor,   // hex without #
    string ValueColor,   // hex without #
    BadgeStyle Style,
    string? LogoPath = null  // 이모지 또는 텍스트 아이콘
);

public static class SvgBadgeBuilder
{
    // DejaVu Sans 폰트 기준 평균 문자 너비 (11px)
    static int TextWidth(string s, bool upper = false)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        string t = upper ? s.ToUpper() : s;
        double w = 0;
        foreach (char c in t)
            w += c is 'i' or 'l' or '1' or '|' or '!' ? 4.5
               : c is 'f' or 'j' or 'r' or 't' ? 6
               : c is 'm' or 'w' ? 12.5
               : c is 'I' ? 5
               : char.IsUpper(c) ? 9
               : 6.5;
        return (int)Math.Ceiling(w);
    }

    public static string Build(BadgeConfig cfg)
    {
        bool upper = cfg.Style == BadgeStyle.ForTheBadge;
        string label = upper ? cfg.Label.ToUpper() : cfg.Label;
        string value = upper ? cfg.Value.ToUpper() : cfg.Value;

        int lw = TextWidth(label, upper) + 20;
        int vw = TextWidth(value, upper) + 20;
        int height = cfg.Style == BadgeStyle.ForTheBadge ? 28
                   : cfg.Style == BadgeStyle.Social ? 20
                   : 20;
        int totalW = lw + vw;
        int radius = cfg.Style is BadgeStyle.Flat or BadgeStyle.Plastic ? 3
                   : cfg.Style == BadgeStyle.Social ? 4
                   : 0;
        string font = cfg.Style == BadgeStyle.ForTheBadge ? "700 11px DejaVu Sans" : "400 11px DejaVu Sans";
        double lx = lw / 2.0;
        double vx = lw + vw / 2.0;
        int ty = (int)(height * 0.67);
        string shadow = $"y=\"{ty + 1}\"";
        string ymain = $"y=\"{ty}\"";

        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" width="{totalW}" height="{height}">""");

        // 클립패스
        if (radius > 0)
            sb.Append($"""<clipPath id="c"><rect width="{totalW}" height="{height}" rx="{radius}" ry="{radius}"/></clipPath>""");

        // 배경
        string clipAttr = radius > 0 ? "clip-path=\"url(#c)\"" : "";
        if (cfg.Style == BadgeStyle.Plastic)
        {
            sb.Append($"""<linearGradient id="g" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>""");
        }
        sb.Append($"""<g {clipAttr}>""");
        sb.Append($"""<rect width="{lw}" height="{height}" fill="#{cfg.LabelColor}"/>""");
        sb.Append($"""<rect x="{lw}" width="{vw}" height="{height}" fill="#{cfg.ValueColor}"/>""");
        if (cfg.Style == BadgeStyle.Plastic)
            sb.Append($"""<rect width="{totalW}" height="{height}" fill="url(#g)"/>""");
        sb.Append("</g>");

        // 텍스트
        sb.Append($"""<g fill="#fff" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="11" text-rendering="geometricPrecision">""");
        if (cfg.Style != BadgeStyle.ForTheBadge)
        {
            // 그림자
            sb.Append($"""<text x="{lx + 0.5}" {shadow} fill="#010101" fill-opacity=".3" text-anchor="middle" {(upper ? "font-weight=\"bold\"" : "")}>{EscapeXml(label)}</text>""");
            sb.Append($"""<text x="{lx}" {ymain} text-anchor="middle" {(upper ? "font-weight=\"bold\"" : "")}>{EscapeXml(label)}</text>""");
            sb.Append($"""<text x="{vx + 0.5}" {shadow} fill="#010101" fill-opacity=".3" text-anchor="middle">{EscapeXml(value)}</text>""");
            sb.Append($"""<text x="{vx}" {ymain} text-anchor="middle">{EscapeXml(value)}</text>""");
        }
        else
        {
            // ForTheBadge: 세로 중앙 정렬
            int dty = height / 2 + 4;
            sb.Append($"""<text x="{lx}" y="{dty}" text-anchor="middle" font-weight="bold" letter-spacing="1">{EscapeXml(label)}</text>""");
            sb.Append($"""<text x="{vx}" y="{dty}" text-anchor="middle" font-weight="bold" letter-spacing="1">{EscapeXml(value)}</text>""");
        }
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    static string EscapeXml(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;")
        .Replace(">", "&gt;").Replace("\"", "&quot;");

    // 미리 정의된 색상 팔레트
    public static readonly (string Name, string Hex)[] Palette =
    [
        ("brightgreen", "4c1"),
        ("green", "97ca00"),
        ("yellow", "dfb317"),
        ("orange", "fe7d37"),
        ("red", "e05d44"),
        ("blue", "007ec6"),
        ("lightgrey", "9f9f9f"),
        ("blueviolet", "8a2be2"),
        ("ff69b4", "ff69b4"),
        ("success", "4c1"),
        ("important", "fe7d37"),
        ("critical", "e05d44"),
        ("informational", "007ec6"),
        ("inactive", "9f9f9f"),
    ];
}
