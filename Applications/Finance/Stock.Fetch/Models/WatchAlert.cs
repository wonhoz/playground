namespace Stock.Fetch.Models;

/// <summary>
/// 관심 종목 추세 알림 1건. 기준값(직전 알림 시점의 등락율) 대비 현재 등락율이 step만큼
/// 변했을 때 발생한다. <see cref="IsStartup"/>이면 모니터링 시작 시 현재 수준을 알리는 1회성 알림이다.
/// </summary>
public sealed record WatchAlert(
    WatchItem Item,
    decimal Price,         // 현재가
    decimal CurrentRate,   // 현재 등락율(전일 대비 %)
    decimal RefRate,       // 기준 등락율(직전 알림 시점 %)
    double Step,           // 트리거된 조건의 변화 단위(%)
    double WindowMinutes,  // 트리거된 조건의 추세 기간(분)
    bool IsStartup,
    DateTime Time,
    string RulesText = "",     // 시작 알림용: 적용 조건 요약("3분당 1%, 5분당 2%")
    // 반등 추정(추세 알림에만 첨부 · 널이면 미첨부)
    double? ReversalProb = null,
    string? ReversalDirText = null,
    string? ReversalText = null)
{
    public string Display => string.IsNullOrEmpty(Item.Name) ? Item.Symbol : $"{Item.Name} ({Item.Symbol})";
    /// <summary>추세 방향: 시작 알림은 등락율 부호, 그 외엔 기준값 대비 변화 부호.</summary>
    public bool IsUp => IsStartup ? CurrentRate >= 0 : CurrentRate >= RefRate;
    /// <summary>기준값 대비 변화량(%포인트, 부호 포함).</summary>
    public decimal Delta => CurrentRate - RefRate;
    public string PriceText => Item.IsIndex ? $"{Price:N2}p"
        : Item.Market == MarketKind.US ? $"${Price:N2}" : $"{Price:N0}원";
}
