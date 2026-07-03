namespace Stock.Fetch.Models;

/// <summary>바닥 반등 시그널 종류. Rebound=1차(밴드 하단 반등), GoldenCross=2차(MA5↗MA20 확인).</summary>
public enum BottomSignalKind { Rebound, GoldenCross }

/// <summary>
/// 바닥 반등 시그널 알림 1건(관심 종목 · 국내 1분봉).
/// 급락 → 볼린저 하단 터치 → 밴드 복귀 + RSI 상승 전환 + 거래량 급증 패턴을 조건화한 결과.
/// </summary>
public sealed record BottomSignal(
    string Code,
    string Name,
    BottomSignalKind Kind,
    decimal Price,
    string Detail,
    DateTime Time)
{
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";
}
