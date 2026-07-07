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
public enum MinuteSignalKind
{
    MorningBrief, Rebound, FollowThrough, GoldenCross, StrongGoldenCross, WeakGoldenCross, TopWarn, DeadCross,
    /// <summary>
    /// 🚀 진입 적기(추세 지속 확인): 골든크로스(✅🔥) 후 N분(기본 2)이 지나도 종가가 GC 가격 이상을
    /// 유지하면 발화 — "무작정 GC에 진입"의 오탐을 거른다. 실측(14일 GC 114건): 즉시 진입 승률 57%·
    /// 오탐율 16% → +2분 지속확인 82%·2%, +3분 90%·0%. 🔥는 즉시 오탐율 38%라 특히 유효.
    /// </summary>
    HoldConfirm,
    /// <summary>
    /// 🔁 전환 확인(교차): X의 고점 경고 후 15분 내 반대 짝 종목(레버리지↔인버스)의 반등 확인(✅🔥)이
    /// 따라온 케이스 — 실측(14일·27건): 30분 내 1% 이상 하락 93% · 평균 낙폭 −4.1%(경고 단독 43~67%·−1.3~−2.6%).
    /// 경고가 확인보다 늦은 역순은 실측 실패라 제외. 라이브 전용(백테스트는 종목 단위라 미발생).
    /// </summary>
    CrossTurn,
    /// <summary>
    /// 📦 진입 권장(박스 상단 돌파): GC/🚀 직후 형성된 가변 박스(시드 3봉 후 계속 확장)의 상단을 종가가
    /// 돌파한 순간 — "흔들림을 통과하고 진입"을 확인. 실측(15일 GC 134건): GC 즉시 진입 순상승 41%·낙폭≤−2% 28%
    /// → 박스 상단 돌파 진입 49%·16%로 승률↑·낙폭 위험 절반. 하단 이탈은 침묵(흔들기 바닥인 경우가 다수).
    /// </summary>
    BoxBreakout
}

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
    string Context = "",       // 판단 보조 컨텍스트(갭·당일·저점比·VWAP·일봉추세) — 분석 창·CSV용
    bool? AboveVwap = null,    // 시그널 봉 종가의 세션 VWAP 상/하 (널=미계산)
    bool Divergence = false,   // RSI 불리시 다이버전스(반등 1차에서 판정 · GC는 1차 값 상속)
    bool ChaseWarn = false,    // ⚠ 흔들림 주의: 종가가 VWAP 깊은 약세(하락 추세 진행 중 · 진입 후 낙폭 큼)
    double StopLossPct = 0,    // 🛑 권장 손절선(%) — 🚀 진입 적기 전용(일반 −2%·흔들림 주의 −3% · 0=미표기)
    double RibbonSpreadPct = double.NaN,  // MA5/20/60/120 리본 스프레드(%) — 밀집=낙폭 작음(NaN=MA120 미형성)
    bool CounterTrend = false) // ⚠ 역추세: MA20·MA60이 동시 하락 중 진입(🚀 전용) — 하락 MA로의 역추세 반등은 실패 잦음
{
    /// <summary>리본 밀집 판정 상한(%): 이하면 "리본 밀집(낙폭 작음)". 실측: 밀집 시그널 평균 낙폭 −0.77%.</summary>
    public const double RibbonTightPct = 1.5;
    /// <summary>리본 분산 판정 하한(%): 이상이면 "⚠ 리본 분산(낙폭 주의)". 실측: 분산 시그널 평균 낙폭 −2.40%.</summary>
    public const double RibbonWidePct = 3.0;

    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";

    /// <summary>하락 계열(고점 경고·데드크로스·전환 확인) 여부 — 트레이 warning 표시 등에 사용.</summary>
    public bool IsBearish => Kind is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross or MinuteSignalKind.CrossTurn;

    /// <summary>알림 구분용 타임프레임 접두 — 1분봉은 생략, 롤링 봉은 "[N분] ".</summary>
    public string TfLabel => Timeframe > 1 ? $"[{Timeframe}분] " : "";

    /// <summary>권장 손절가(원). StopLossPct가 있을 때만(진입 적기). 원 단위 반올림.</summary>
    public decimal StopLossPrice => StopLossPct > 0 ? Math.Round(Price * (1 - (decimal)StopLossPct / 100)) : 0;

    /// <summary>
    /// 종합 판정 단계(별 개수). 진입 적기(🌟×5)만 최상위이고 GC 이하는 한 단계씩 내려 "확인은 관심, 진입은 🚀에서"를 강조.
    /// 참고(0·별 없음) → 관찰(1) → 주목(2) → 좋음(3) → 최상·즉시 주목(4) → 진입 적기(5·🌟).
    /// 실측(14일×3종목 · 이후 30분 ±1% 선도달) 근거: 🚀 90% / 🔥 63%·건당 +4.6% / ✅+VWAP위 71%·낙폭 −0.97% /
    /// ✅ 50% / ↗ 44% / 📈 50% / ⚠ 49%(수익폭 열위). 0=참고 또는 등급 없음(브리핑·하락 계열).
    /// </summary>
    public int Stars => Kind switch
    {
        MinuteSignalKind.HoldConfirm => 5,          // 🌟 진입 적기 — 실측 승률 90%(즉시 57%)
        MinuteSignalKind.BoxBreakout => 5,          // 📦 진입 권장 — 박스 상단 돌파(실측 순상승 49%·낙폭 위험 절반)
        MinuteSignalKind.StrongGoldenCross => 4,    // 최상·즉시 주목(🔥 강력 GC)
        MinuteSignalKind.GoldenCross => AboveVwap == true ? 4 : 3,   // VWAP위=최상 / 아래=좋음
        MinuteSignalKind.FollowThrough => 2,        // 주목
        MinuteSignalKind.Rebound => Divergence ? 2 : 1,   // 다이버전스=주목 / 기본=관찰
        MinuteSignalKind.WeakGoldenCross => 0,      // 참고(별 없음 · 횡보성)
        _ => 0
    };

    /// <summary>
    /// Slack·트레이 알림 2번째 줄 — 판정 핵심만(별점 + 짧은 근거 + 태그). 종류명은 1번째 줄(타이틀)에 이미 있으므로
    /// 여기서는 <b>중복 없이</b> 판정·근거·경고만 담는다. 지표 수치 상세는 시그널 로그 CSV·분석 창.
    /// </summary>
    public string VerdictLine
    {
        get
        {
            if (Kind == MinuteSignalKind.MorningBrief) return Detail;
            // 🚀 진입 적기 / 📦 진입 권장 — 둘 다 🌟×5(타이틀이 종류를 구분). 근거 한 줄만.
            if (Kind == MinuteSignalKind.HoldConfirm)
                return CounterTrend
                    ? "🌟🌟🌟🌟🌟 ⚠ 역추세 — MA20·60 하락 중 (정렬 75% vs 역추세 48%)"
                    : "🌟🌟🌟🌟🌟 실측 승률 90% (즉시진입 57%)";
            if (Kind == MinuteSignalKind.BoxBreakout)
                return "🌟🌟🌟🌟🌟 흔들림 통과 · 순상승 49% (GC 41%)" + (ChaseWarn ? " · ⚠ 흔들림 주의" : "");
            if (Kind == MinuteSignalKind.CrossTurn) return "🔻 매도 타이밍 · 30분 내 93% 하락 (평균 −4.1%)";
            if (Kind == MinuteSignalKind.TopWarn) return "⚠️ 익절 검토 · 과열 이탈";
            if (Kind == MinuteSignalKind.DeadCross) return "🔻 반등 무효 · 정리/관망";

            // 별 표기: 참고(0)=별 없음 · 관찰~최상(1~4)=⭐. 타이틀의 종류명("반등 확인" 등)은 반복하지 않는다.
            string stars = new string('⭐', Stars);
            string label = Stars switch
            {
                4 => "최상·즉시 주목 · 2분 더 보고 진입",   // 🔥강력 GC / ✅ GC+VWAP위 — 즉시 진입은 오탐(16~38%)
                3 => "좋음 · 2분 더 보고 진입",
                2 => Kind == MinuteSignalKind.FollowThrough ? "주목 · 흐름 지속" : "주목 · 1차 후보 · 확인 대기",
                1 => "관찰 · 1차 후보 · 확인 대기",
                _ => "참고 · 무시 가능",
            };
            var tags = new List<string>(4);
            if (ChaseWarn) tags.Add("⚠ 흔들림 주의");   // 위험 표시를 맨 앞에
            if (!double.IsNaN(RibbonSpreadPct))
            {
                if (RibbonSpreadPct <= RibbonTightPct) tags.Add($"리본 밀집 {RibbonSpreadPct:0.0}%");
                else if (RibbonSpreadPct >= RibbonWidePct) tags.Add($"⚠ 리본 분산 {RibbonSpreadPct:0.0}%");
            }
            if (AboveVwap == true) tags.Add("VWAP 위");
            if (Divergence) tags.Add("다이버전스");
            string prefix = stars.Length > 0 ? stars + " " : "";
            return $"{prefix}{label}{(tags.Count > 0 ? " · " + string.Join(" · ", tags) : "")}";
        }
    }
}
