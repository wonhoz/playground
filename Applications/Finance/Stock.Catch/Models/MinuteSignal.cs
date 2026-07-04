namespace Stock.Catch.Models;

/// <summary>
/// 1분봉 시그널 종류.
/// Rebound=바닥 반등(볼린저 하단 반등), FollowThrough=반등 지속(1차 직후 봉 양봉 · 조기 확인),
/// GoldenCross=반등 확인(MA5↗MA20 + 모멘텀 충분), WeakGoldenCross=약한 확인(크로스는 떴지만
/// 1차 이후 상승률 임계 미달 — 횡보성 크로스 · 하락 추세 속 반등 위험),
/// TopWarn=고점 경고(볼린저 상단 이탈), DeadCross=하락 확인(MA5↘MA20).
/// 실측(0193T0 07.02~03): 진짜 반등은 직후 양봉 3/4·GC 4/4, 가짜는 직후 양봉 1/3·GC 1/3.
/// GC 5건 중 가짜 1건(07-02 12:35)은 1차→GC 상승률 +0.51%로 진짜(+0.90~6.73%)와 분리 —
/// 모멘텀 임계(기본 0.8%)가 강/약 확인을 가른다(트리거 봉 장대양봉은 오히려 가짜에서 큼).
/// </summary>
public enum MinuteSignalKind { MorningBrief, Rebound, FollowThrough, GoldenCross, StrongGoldenCross, WeakGoldenCross, TopWarn, DeadCross }

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
    DateTime Time,
    int Timeframe = 1,
    string Context = "")   // 판단 보조 컨텍스트(갭·당일·저점比·일봉추세) — Slack 3번째 줄
{
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";

    /// <summary>하락 계열(고점 경고·데드크로스) 여부 — 트레이 warning 표시 등에 사용.</summary>
    public bool IsBearish => Kind is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross;

    /// <summary>알림 구분용 타임프레임 접두 — 1분봉은 생략, 롤링 봉은 "[N분] ".</summary>
    public string TfLabel => Timeframe > 1 ? $"[{Timeframe}분] " : "";
}
