namespace Stock.Fetch.Models;

/// <summary>
/// 1분봉 시그널 종류.
/// Rebound=바닥 반등(볼린저 하단 반등), FollowThrough=반등 지속(1차 직후 봉 양봉 · 조기 확인),
/// GoldenCross=반등 확인(MA5↗MA20), TopWarn=고점 경고(볼린저 상단 이탈), DeadCross=하락 확인(MA5↘MA20).
/// 실측(0193T0 07.02~03): 진짜 반등은 직후 양봉 3/4·GC 4/4, 가짜는 직후 양봉 1/3·GC 1/3 —
/// 직후 양봉·GC가 강/약 반등을 가르는 확인 신호다(트리거 봉 장대양봉은 오히려 가짜에서 큼).
/// </summary>
public enum MinuteSignalKind { Rebound, FollowThrough, GoldenCross, TopWarn, DeadCross }

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
