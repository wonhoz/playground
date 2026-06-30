using System.Globalization;

namespace Stock.Fetch.Models;

/// <summary>
/// 추세 알림 조건 1개: <see cref="WindowMinutes"/>분 동안 기준값 대비 <see cref="StepPercent"/>% 이상
/// 변하면 알림. 한 종목에 여러 조건(예: 3분당 1%, 5분당 2%)을 동시에 걸 수 있다.
/// </summary>
public sealed class TrendRule
{
    public double WindowMinutes { get; set; } = 3;
    public double StepPercent { get; set; } = 2;

    /// <summary>상태 추적용 고유 키(기간/단위 조합).</summary>
    public string Key => $"{WindowMinutes:0.###}/{StepPercent:0.###}";
    public override string ToString() => $"{WindowMinutes:0.#}분당 {StepPercent:0.###}%";

    // ── 입력/저장 텍스트 변환 ("분:%" 쉼표 구분, 예: "3:1, 5:2") ──
    public static string ToText(IEnumerable<TrendRule> rules)
        => string.Join(", ", rules.Select(r =>
            $"{r.WindowMinutes.ToString("0.###", CultureInfo.InvariantCulture)}:{r.StepPercent.ToString("0.###", CultureInfo.InvariantCulture)}"));

    /// <summary>"3:1, 5:2" → 규칙 목록. 잘못된 토큰은 무시. 유효 규칙이 없으면 빈 목록.</summary>
    public static List<TrendRule> Parse(string text)
    {
        var list = new List<TrendRule>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var token in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':', '/');
            if (parts.Length != 2) continue;
            if (double.TryParse(parts[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var win) && win > 0 &&
                double.TryParse(parts[1].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var step) && step > 0)
            {
                // 같은 기간/단위 중복 제거
                if (!list.Any(r => Math.Abs(r.WindowMinutes - win) < 1e-9 && Math.Abs(r.StepPercent - step) < 1e-9))
                    list.Add(new TrendRule { WindowMinutes = win, StepPercent = step });
            }
        }
        return list;
    }

    /// <summary>읽기용 요약("3분당 1%, 5분당 2%"). 빈 목록이면 dash.</summary>
    public static string Summary(IEnumerable<TrendRule> rules)
    {
        var s = string.Join(", ", rules.Select(r => r.ToString()));
        return string.IsNullOrEmpty(s) ? "—" : s;
    }
}
