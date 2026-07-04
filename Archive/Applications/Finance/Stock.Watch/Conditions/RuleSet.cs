using Stock.Watch.Indicators;
using Stock.Watch.Models;

namespace Stock.Watch.Conditions;

public enum CombineMode
{
    All,  // 모든 조건 충족(AND)
    Any   // 하나라도 충족(OR)
}

/// <summary>
/// 한 종목의 매수 또는 매도 룰셋. 여러 <see cref="Condition"/>을 AND/OR로 결합한다.
/// </summary>
public sealed class RuleSet
{
    public RuleKind Kind { get; set; } = RuleKind.Buy;
    public bool Enabled { get; set; } = true;
    public CombineMode Combine { get; set; } = CombineMode.All;
    public List<Condition> Conditions { get; set; } = new();

    public bool Evaluate(IndicatorSet set, int index)
    {
        if (!Enabled || Conditions.Count == 0) return false;
        return Combine == CombineMode.All
            ? Conditions.All(c => c.Evaluate(set, index))
            : Conditions.Any(c => c.Evaluate(set, index));
    }

    /// <summary>UI·Slack 표시용 요약. 예) "RSI &lt; 30  그리고  현재가 ≤ 볼린저하단".</summary>
    public string Summary()
    {
        if (Conditions.Count == 0) return "(조건 없음)";
        string sep = Combine == CombineMode.All ? "  그리고  " : "  또는  ";
        return string.Join(sep, Conditions.Select(c => c.Summary()));
    }
}
