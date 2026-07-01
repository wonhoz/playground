using System.Globalization;

namespace Stock.Fetch.Models;

/// <summary>
/// 추세 알림 조건 1개: <see cref="WindowMinutes"/>분 동안 기준값 대비 상승 <see cref="StepUp"/>% 또는
/// 하락 <see cref="StepDown"/>% 이상이면 알림. 한 종목에 여러 조건(예: 3분당 ↑1%/↓2%, 5분당 2%)을 동시에 걸 수 있다.
/// </summary>
public sealed class TrendRule
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public double WindowMinutes { get; set; } = 3;
    /// <summary>상승 임계값(%).</summary>
    public double StepUp { get; set; } = 2;
    /// <summary>하락 임계값(%).</summary>
    public double StepDown { get; set; } = 2;

    /// <summary>하위호환: 기존 config의 StepPercent를 읽으면 상승/하락에 동일 적용(직렬화는 안 함 — set 전용).</summary>
    public double StepPercent { set { StepUp = value; StepDown = value; } }

    /// <summary>상승·하락 임계값이 같은지.</summary>
    public bool Symmetric => Math.Abs(StepUp - StepDown) < 1e-9;

    /// <summary>상태 추적용 고유 키(기간/상승/하락 조합).</summary>
    public string Key => $"{WindowMinutes:0.###}/{StepUp:0.###}/{StepDown:0.###}";

    public override string ToString() => Symmetric
        ? $"{WindowMinutes:0.#}분당 {StepUp:0.###}%"
        : $"{WindowMinutes:0.#}분당 ↑{StepUp:0.###}% ↓{StepDown:0.###}%";

    // ── 입력/저장 텍스트 변환 ("기간:상승[:하락]" 쉼표 구분, 예: "3:1:2, 5:2") ──
    public static string ToText(IEnumerable<TrendRule> rules) => string.Join(", ", rules.Select(TokenOf));

    private static string TokenOf(TrendRule r)
    {
        string w = r.WindowMinutes.ToString("0.###", Inv);
        string up = r.StepUp.ToString("0.###", Inv);
        return r.Symmetric ? $"{w}:{up}" : $"{w}:{up}:{r.StepDown.ToString("0.###", Inv)}";
    }

    /// <summary>"3:1:2, 5:2" → 규칙 목록. 기간:상승[:하락]. 하락 생략 시 상승과 동일. 잘못된 토큰은 무시.</summary>
    public static List<TrendRule> Parse(string text)
    {
        var list = new List<TrendRule>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var token in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':');
            if (parts.Length is < 2 or > 3) continue;
            if (!TryD(parts[0], out var win) || win <= 0) continue;
            if (!TryD(parts[1], out var up) || up <= 0) continue;
            double down = up;
            if (parts.Length == 3 && (!TryD(parts[2], out down) || down <= 0)) continue;

            if (!list.Any(r => Math.Abs(r.WindowMinutes - win) < 1e-9 &&
                               Math.Abs(r.StepUp - up) < 1e-9 && Math.Abs(r.StepDown - down) < 1e-9))
                list.Add(new TrendRule { WindowMinutes = win, StepUp = up, StepDown = down });
        }
        return list;
    }

    private static bool TryD(string s, out double v)
        => double.TryParse(s.Trim(), NumberStyles.Number, Inv, out v);

    /// <summary>읽기용 요약("3분당 ↑1% ↓2%, 5분당 2%"). 빈 목록이면 dash.</summary>
    public static string Summary(IEnumerable<TrendRule> rules)
    {
        var s = string.Join(", ", rules.Select(r => r.ToString()));
        return string.IsNullOrEmpty(s) ? "—" : s;
    }
}
