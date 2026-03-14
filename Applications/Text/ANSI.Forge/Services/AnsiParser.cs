using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace AnsiForge.Services;

// VT100/256색/트루컬러 ANSI 이스케이프 파서
public partial class AnsiParser
{
    [GeneratedRegex(@"\x1b\[([0-9;]*)m")]
    private static partial Regex SgrRegex();

    // 기본 16색 팔레트
    static readonly Color[] Colors16 =
    [
        Color.FromRgb(0,0,0),        Color.FromRgb(128,0,0),
        Color.FromRgb(0,128,0),      Color.FromRgb(128,128,0),
        Color.FromRgb(0,0,128),      Color.FromRgb(128,0,128),
        Color.FromRgb(0,128,128),    Color.FromRgb(192,192,192),
        Color.FromRgb(128,128,128),  Color.FromRgb(255,0,0),
        Color.FromRgb(0,255,0),      Color.FromRgb(255,255,0),
        Color.FromRgb(0,0,255),      Color.FromRgb(255,0,255),
        Color.FromRgb(0,255,255),    Color.FromRgb(255,255,255),
    ];

    // xterm 256색 팔레트 생성
    static readonly Color[] Colors256 = BuildXterm256();

    static Color[] BuildXterm256()
    {
        var palette = new Color[256];
        // 0-15: 기본 16색
        for (int i = 0; i < 16; i++) palette[i] = Colors16[i];
        // 16-231: 6x6x6 색상 큐브
        for (int i = 0; i < 216; i++)
        {
            int b = i % 6, g = (i / 6) % 6, r = i / 36;
            palette[16 + i] = Color.FromRgb(
                (byte)(r == 0 ? 0 : 55 + r * 40),
                (byte)(g == 0 ? 0 : 55 + g * 40),
                (byte)(b == 0 ? 0 : 55 + b * 40));
        }
        // 232-255: 회색조
        for (int i = 0; i < 24; i++)
            palette[232 + i] = Color.FromRgb((byte)(8 + i * 10), (byte)(8 + i * 10), (byte)(8 + i * 10));
        return palette;
    }

    public static AnsiSpan[] Parse(string text)
    {
        var spans = new List<AnsiSpan>();
        int pos = 0;

        Color fg = Color.FromRgb(0xCC, 0xCC, 0xCC);
        Color bg = Color.FromRgb(0, 0, 0);
        bool bold = false, underline = false;

        foreach (Match m in SgrRegex().Matches(text))
        {
            // 이스케이프 시퀀스 이전 텍스트
            if (m.Index > pos)
            {
                var rawText = text[pos..m.Index];
                spans.Add(new AnsiSpan(rawText, fg, bg, bold, underline));
            }
            pos = m.Index + m.Length;

            // SGR 파라미터 파싱
            var parts = m.Groups[1].Value.Split(';').Where(p => !string.IsNullOrEmpty(p)).Select(int.Parse).ToList();
            if (parts.Count == 0) parts.Add(0);

            for (int i = 0; i < parts.Count; i++)
            {
                int code = parts[i];
                switch (code)
                {
                    case 0: fg = Color.FromRgb(0xCC, 0xCC, 0xCC); bg = Color.FromRgb(0, 0, 0); bold = underline = false; break;
                    case 1: bold = true; break;
                    case 4: underline = true; break;
                    case 5: break; // blink — 미지원
                    case 7: break; // reverse video — 시각적으로 무시
                    case 22: bold = false; break;
                    case 24: underline = false; break;
                    case >= 30 and <= 37: fg = Colors16[code - 30 + (bold ? 8 : 0)]; break;
                    case 38 when i + 2 < parts.Count && parts[i + 1] == 5:
                        fg = Colors256[Math.Clamp(parts[i + 2], 0, 255)]; i += 2; break;
                    case 38 when i + 4 < parts.Count && parts[i + 1] == 2:
                        fg = Color.FromRgb((byte)parts[i + 2], (byte)parts[i + 3], (byte)parts[i + 4]); i += 4; break;
                    case 39: fg = Color.FromRgb(0xCC, 0xCC, 0xCC); break;
                    case >= 40 and <= 47: bg = Colors16[code - 40]; break;
                    case 48 when i + 2 < parts.Count && parts[i + 1] == 5:
                        bg = Colors256[Math.Clamp(parts[i + 2], 0, 255)]; i += 2; break;
                    case 48 when i + 4 < parts.Count && parts[i + 1] == 2:
                        bg = Color.FromRgb((byte)parts[i + 2], (byte)parts[i + 3], (byte)parts[i + 4]); i += 4; break;
                    case 49: bg = Color.FromRgb(0, 0, 0); break;
                    case >= 90 and <= 97: fg = Colors16[code - 90 + 8]; break;
                    case >= 100 and <= 107: bg = Colors16[code - 100 + 8]; break;
                }
            }
        }

        // 남은 텍스트
        if (pos < text.Length)
            spans.Add(new AnsiSpan(text[pos..], fg, bg, bold, underline));

        return [.. spans];
    }

    public static string ToHtml(AnsiSpan[] spans)
    {
        var sb = new StringBuilder("<pre style=\"background:#000;font-family:monospace;\">");
        foreach (var s in spans)
        {
            string fg = $"#{s.Foreground.R:X2}{s.Foreground.G:X2}{s.Foreground.B:X2}";
            string bg = $"#{s.Background.R:X2}{s.Background.G:X2}{s.Background.B:X2}";
            string style = $"color:{fg};background:{bg};";
            if (s.Bold) style += "font-weight:bold;";
            if (s.Underline) style += "text-decoration:underline;";
            sb.Append($"<span style=\"{style}\">{System.Web.HttpUtility.HtmlEncode(s.Text)}</span>");
        }
        sb.Append("</pre>");
        return sb.ToString();
    }
}

public record AnsiSpan(string Text, Color Foreground, Color Background, bool Bold, bool Underline);
