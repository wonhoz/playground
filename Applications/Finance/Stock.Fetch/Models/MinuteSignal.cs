namespace Stock.Fetch.Models;

/// <summary>
/// 1분봉 시그널 종류.
/// Rebound=바닥 반등(볼린저 하단 반등), GoldenCross=반등 확인(MA5↗MA20),
/// TopWarn=고점 경고(볼린저 상단 이탈), DeadCross=하락 확인(MA5↘MA20).
/// </summary>
public enum MinuteSignalKind { Rebound, GoldenCross, TopWarn, DeadCross }

/// <summary>
/// 1분봉 시그널 알림 1건(관심 종목 · 국내).
/// 바닥: 급락 → 볼린저 하단 터치 → 밴드 복귀 + RSI 상승 전환 + 거래량 급증.
/// 고점: 상단 밴드워킹 → 밴드 안 복귀 마감 + RSI 과매수 하향 전환 + 소진 증거(윗꼬리/거래량).
/// </summary>
public sealed record MinuteSignal(
    string Code,
    string Name,
    MinuteSignalKind Kind,
    decimal Price,
    string Detail,
    DateTime Time)
{
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";

    /// <summary>하락 계열(고점 경고·데드크로스) 여부 — 트레이 warning 표시 등에 사용.</summary>
    public bool IsBearish => Kind is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross;
}
