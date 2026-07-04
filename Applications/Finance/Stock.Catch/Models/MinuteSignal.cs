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
    /// 🔁 전환 확인(교차): X의 고점 경고 후 15분 내 반대 짝 종목(레버리지↔인버스)의 반등 확인(✅🔥)이
    /// 따라온 케이스 — 실측(14일·27건): 30분 내 1% 이상 하락 93% · 평균 낙폭 −4.1%(경고 단독 43~67%·−1.3~−2.6%).
    /// 경고가 확인보다 늦은 역순은 실측 실패라 제외. 라이브 전용(백테스트는 종목 단위라 미발생).
    /// </summary>
    CrossTurn
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
    bool Divergence = false)   // RSI 불리시 다이버전스(반등 1차에서 판정 · GC는 1차 값 상속)
{
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";

    /// <summary>하락 계열(고점 경고·데드크로스·전환 확인) 여부 — 트레이 warning 표시 등에 사용.</summary>
    public bool IsBearish => Kind is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross or MinuteSignalKind.CrossTurn;

    /// <summary>알림 구분용 타임프레임 접두 — 1분봉은 생략, 롤링 봉은 "[N분] ".</summary>
    public string TfLabel => Timeframe > 1 ? $"[{Timeframe}분] " : "";

    /// <summary>
    /// 종합 판정 5단계(★). 실측(14일×3종목 · 이후 30분 ±1% 선도달) 근거:
    /// 🔥 63%·건당 +4.6% / ✅+VWAP위 71%·낙폭 −0.97% / ✅ 50% / ↗ 44% / 📈 50% / ⚠ 49%(수익폭 열위).
    /// 0=등급 없음(브리핑·하락 계열).
    /// </summary>
    public int Stars => Kind switch
    {
        MinuteSignalKind.StrongGoldenCross => 5,
        MinuteSignalKind.GoldenCross => AboveVwap == true ? 5 : 4,
        MinuteSignalKind.FollowThrough => 3,
        MinuteSignalKind.Rebound => Divergence ? 3 : 2,
        MinuteSignalKind.WeakGoldenCross => 1,
        _ => 0
    };

    /// <summary>Slack·트레이용 한 줄 종합 판정 — 별점 + 라벨 (+ 핵심 태그). 지표 수치는 분석 창에서.</summary>
    public string VerdictLine
    {
        get
        {
            if (Kind == MinuteSignalKind.CrossTurn) return "🔁 전환 확인 — 매도 타이밍 (실측 93% 하락 · 30분 평균 −4.1%)";
            if (Kind == MinuteSignalKind.TopWarn) return "⚠️ 경계 — 과열 이탈 · 보유 중이면 익절 검토";
            if (Kind == MinuteSignalKind.DeadCross) return "🔻 하락 확인 — 반등 무효 · 정리/관망";
            if (Kind == MinuteSignalKind.MorningBrief) return Detail;

            // 최상(5성)은 🌟로 차별화 — 모바일 알림에서 즉시 구분되도록.
            string stars = Stars == 5 ? "🌟🌟🌟🌟🌟" : new string('⭐', Stars);
            string label = Stars switch
            {
                5 => Kind == MinuteSignalKind.StrongGoldenCross
                    ? "최상 — 즉시 주목 (실측 ~63% · 건당 +4.6%)"
                    : "최상 — 즉시 주목 (실측 ~71% · 낙폭 얕음)",
                4 => "좋음 — 진입 검토",
                3 => Kind == MinuteSignalKind.FollowThrough ? "주목 — 반등 흐름 지속" : "주목 — 확인(GC) 대기",
                2 => "관찰 — 1차 후보 · 확인 대기",
                _ => "참고 — 횡보성 · 무시 가능",
            };
            var tags = new List<string>(2);
            if (AboveVwap == true) tags.Add("VWAP 위");
            if (Divergence) tags.Add("다이버전스");
            return $"{stars} {label}{(tags.Count > 0 ? " · " + string.Join(" · ", tags) : "")}";
        }
    }
}
