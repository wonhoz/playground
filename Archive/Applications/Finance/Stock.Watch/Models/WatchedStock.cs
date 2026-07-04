using System.Text.Json.Serialization;
using Stock.Watch.Conditions;

namespace Stock.Watch.Models;

/// <summary>
/// 감시 대상 종목 1개. 종목코드·이름과 매수/매도 룰셋, 알림 도배 방지용 쿨다운 상태를 보관한다.
/// </summary>
public sealed class WatchedStock
{
    public required string Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public RuleSet BuyRules { get; set; } = new() { Kind = RuleKind.Buy };
    public RuleSet SellRules { get; set; } = new() { Kind = RuleKind.Sell };

    // ── 런타임 상태(직렬화 제외) ──
    [JsonIgnore] public decimal LastPrice { get; set; }
    [JsonIgnore] public decimal LastChangeRate { get; set; }
    [JsonIgnore] public DateTime LastBuyAlertAt { get; set; } = DateTime.MinValue;
    [JsonIgnore] public DateTime LastSellAlertAt { get; set; } = DateTime.MinValue;

    /// <summary>직전 평가에서 룰이 참이었는지(엣지 트리거 판정용).</summary>
    [JsonIgnore] public bool BuyWasTrue { get; set; }
    [JsonIgnore] public bool SellWasTrue { get; set; }

    public string Display => string.IsNullOrWhiteSpace(Name) ? Code : $"{Name} ({Code})";
}
