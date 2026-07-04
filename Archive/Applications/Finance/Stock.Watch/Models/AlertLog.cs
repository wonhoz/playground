namespace Stock.Watch.Models;

/// <summary>
/// 조건 충족 시 발생한 알림 1건의 기록(앱 내 로그 + Slack 전송용).
/// </summary>
public sealed class AlertLog
{
    public DateTime Time { get; init; } = DateTime.Now;
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required RuleKind Kind { get; init; }
    public required string RuleSummary { get; init; }
    public decimal Price { get; init; }

    /// <summary>알림 시점의 주요 지표값 스냅샷(표시·Slack 본문용).</summary>
    public string IndicatorDetail { get; init; } = string.Empty;

    public string TimeText => Time.ToString("HH:mm:ss");
    public string KindText => Kind == RuleKind.Buy ? "매수" : "매도";
}

public enum RuleKind
{
    Buy,
    Sell
}
