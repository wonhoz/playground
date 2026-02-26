using System.Windows.Media;

namespace CharArt.Services;

/// <summary>
/// 문자셋 프리셋 정의 및 동적 밀도 계산
/// </summary>
public static class CharSetLibrary
{
    // ── 프리셋 정의 ──────────────────────────────────────────────────

    public static readonly string[] PresetNames =
    [
        "ASCII 기본",
        "ASCII 고밀도",
        "블록",
        "숫자",
        "한글",
        "한자",
        "커스텀",
    ];

    /// <summary>true이면 전각(Full-Width) 문자 — 종횡비 자동 보정 필요</summary>
    public static bool IsFullWidth(string name) =>
        name is "한글" or "한자";

    /// <summary>true이면 동적 밀도 계산 필요 (한글/한자/커스텀)</summary>
    public static bool NeedsDynamic(string name) =>
        name is "한글" or "한자" or "커스텀";

    // ── 사전정렬 문자셋 (밀한 순서) ──────────────────────────────────

    private static readonly char[] AsciiBasic =
        "@#S%?*+;:,. ".ToCharArray();

    private static readonly char[] AsciiDense =
        "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^`'. ".ToCharArray();

    private static readonly char[] Block =
        "█▓▒░ ".ToCharArray();

    private static readonly char[] Digits =
        "9876543210 ".ToCharArray();

    // ── 동적 계산용 풀 ────────────────────────────────────────────────

    private static readonly char[] HangulPool =
        "힘흥흑흡험헌향해학폼폴포피팔틱틴태켜질진조좌주줄제재의음이을나가라마바사아차카다하인".ToCharArray();

    private static readonly char[] HanjaPool =
        "鑫龘靉爨灩囍籲贏響靈聽體驗讀議學謝謙禮義美麗智慧力心文字人山水木火土金日月一二三".ToCharArray();

    // ── 사전정렬 조회 ─────────────────────────────────────────────────

    /// <summary>
    /// 사전정렬 문자셋을 반환. 동적 계산이 필요한 경우 null 반환.
    /// </summary>
    public static char[]? GetPreset(string name) => name switch
    {
        "ASCII 기본"  => AsciiBasic,
        "ASCII 고밀도" => AsciiDense,
        "블록"        => Block,
        "숫자"        => Digits,
        _             => null,
    };

    /// <summary>
    /// 동적 밀도 계산: 각 문자를 렌더링해 밝기 기준으로 내림차순(밀 → 공백) 정렬.
    /// UI 스레드에서 호출해야 한다.
    /// </summary>
    public static char[] ComputeDynamic(string name, string customChars,
                                        string fontFamily, double fontSize)
    {
        var pool = name switch
        {
            "한글"  => HangulPool,
            "한자"  => HanjaPool,
            "커스텀" => (customChars.Length > 0 ? customChars.Distinct().ToArray() : AsciiBasic),
            _      => AsciiBasic,
        };

        var typeface = new Typeface(fontFamily);
        const double dpi = 96.0;

        // 렌더링 크기: 폰트 크기 기반
        int w = Math.Max(4, (int)Math.Ceiling(fontSize * 1.5));
        int h = Math.Max(4, (int)Math.Ceiling(fontSize * 2.5));

        var densities = new List<(char Ch, float Density)>(pool.Length);

        foreach (var ch in pool)
        {
            var ft = new System.Windows.Media.FormattedText(
                ch.ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.White,
                dpi);

            var rtb = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
            var dv  = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w, h));
                ctx.DrawText(ft, new Point(0, 0));
            }
            rtb.Render(dv);

            var pixels = new byte[w * h * 4]; // Pbgra32 = 4 bytes/pixel
            rtb.CopyPixels(pixels, w * 4, 0);

            // R채널(=B채널=G채널, 흰색이므로) > 64 인 픽셀 비율
            int bright = 0;
            for (int i = 2; i < pixels.Length; i += 4) // R 채널 위치(BGRA)
                if (pixels[i] > 64) bright++;

            densities.Add((ch, (float)bright / (w * h)));
        }

        // 공백 문자 추가 (밀도 = 0)
        densities.Add((' ', 0f));

        // 밀도 내림차순 (밀한 문자 → 빈 문자)
        return densities.OrderByDescending(d => d.Density)
                        .Select(d => d.Ch)
                        .Distinct()
                        .ToArray();
    }
}
