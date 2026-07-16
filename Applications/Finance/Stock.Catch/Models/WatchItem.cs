namespace Stock.Catch.Models;

/// <summary>관심 종목의 시장 구분.</summary>
public enum MarketKind { KR, US }

/// <summary>
/// 관심 종목 시세 소스. KR=Naver(무인증·지연)/Kis(실시간),
/// US=Yahoo(지연)/Finnhub(실시간·무료키)/Alpaca(실시간 IEX·무료키)/Kis(준실시간).
/// </summary>
public enum WatchSource { Naver, Yahoo, Kis, Finnhub, Alpaca, Databento }

/// <summary>
/// 관심 종목(워치리스트) 1건. 국내는 6자리 코드, 미국은 알파벳 티커(TSLA·SOXL 등).
/// 미국 + KIS 소스일 때만 거래소 코드(NAS/NYS/AMS)가 필요하다.
/// </summary>
public sealed class WatchItem
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MarketKind Market { get; set; } = MarketKind.KR;
    public WatchSource Source { get; set; } = WatchSource.Naver;
    /// <summary>미국 + KIS 소스용 거래소 코드(NAS=나스닥, NYS=뉴욕, AMS=아멕스/Arca). 그 외 빈값.</summary>
    public string Exchange { get; set; } = "NAS";

    /// <summary>국내 업종/지수 여부(코스피 0001·코스닥 1001·코스피200 2001 등). KIS 지수 엔드포인트로 조회한다.</summary>
    public bool IsIndex { get; set; } = false;

    /// <summary>이 종목 전용 추세 조건 목록. 비어 있으면 전역 설정(WatchRules)을 사용한다.</summary>
    public List<TrendRule> Rules { get; set; } = new();

    /// <summary>추세 알림 방향 필터(둘 다 기본 true). false면 해당 방향 추세 알림을 보내지 않는다.</summary>
    public bool AlertUp { get; set; } = true;
    public bool AlertDown { get; set; } = true;

    /// <summary>매수 래더·갭다운 알림 사용(국내 종목만 의미). 기본 꺼짐.</summary>
    public bool LadderAlert { get; set; } = false;

    /// <summary>바닥 반등 시그널 알림 사용(국내 종목만 · KIS 1분봉 필요). 볼린저 하단 반등 + RSI·거래량 조건. 기본 꺼짐.</summary>
    public bool BottomAlert { get; set; } = false;

    /// <summary>고점 경고 시그널 알림 사용(국내 종목만 · KIS 1분봉 필요). 볼린저 상단 밴드워킹 이탈 + RSI 과매수 전환. 기본 꺼짐.</summary>
    public bool TopAlert { get; set; } = false;

    /// <summary>
    /// 🔊 거래량 급증 알림 사용(국내 종목만 · KIS 1분봉 필요). 완성 1분봉 거래량이 직전 20봉 평균의
    /// RVOL 배수(전역 <c>VolumeSurgeRvol</c> 또는 종목 override) 이상이면 방향 무관 관심 알림. 기본 꺼짐.
    /// </summary>
    public bool VolumeSurgeAlert { get; set; } = false;

    /// <summary>이 종목 전용 Slack 채널(예: "#stock-sk"). 비우면 전역 기본 채널(설정의 SlackChannel)을 사용한다.</summary>
    public string SlackChannel { get; set; } = string.Empty;

    /// <summary>
    /// 반대 방향 짝 종목 코드(레버리지↔인버스 · 예: 0193T0의 짝 = 0197X0). 지정하면 이 종목의
    /// 고점 경고 후 15분 내 짝 종목 반등 확인(✅🔥)이 뜰 때 "🔁 전환 확인" 교차 알림을 보낸다
    /// (실측 14일: 30분 내 1% 이상 하락 93% · 평균 −4.1%). 비우면 교차 알림 없음.
    /// </summary>
    public string PairSymbol { get; set; } = string.Empty;

    // ── 종목별 시그널 파라미터 override (null=전역 설정 사용) ──
    // 종목마다 변동성·성격이 달라 전역 하나로는 최적이 아니므로, 필요한 종목만 개별 지정한다.
    // 예: 인버스·저변동 종목은 fake 반등이 많아 RSI/거래량/%b를 강화(정밀), 레버리지·본주는 전역(표준).
    /// <summary>바닥 셋업 RSI 과매도 상한(전역 BottomRsiMax override). 낮출수록 더 깊은 과매도만 인정.</summary>
    public double? BottomRsiMax { get; set; }
    /// <summary>바닥 거래량 급증 배수 override. 높일수록 관심 집중된 반등만 인정.</summary>
    public double? BottomVolumeRatio { get; set; }
    /// <summary>바닥 밴드워킹 허용 터치 상한 override.</summary>
    public int? BottomWalkMaxTouches { get; set; }
    /// <summary>바닥 트리거 봉 최소 %b override. 높일수록 확실한 밴드 복귀만(약반등·fake 억제).</summary>
    public double? BottomMinPercentB { get; set; }
    /// <summary>골든크로스 '강' 인정 모멘텀(%) override.</summary>
    public double? BottomGcMinRisePct { get; set; }
    /// <summary>골든크로스 '강력🔥' 모멘텀(%) override.</summary>
    public double? BottomGcStrongPct { get; set; }
    /// <summary>고점 셋업 RSI 과매수 하한 override.</summary>
    public double? TopRsiMin { get; set; }
    /// <summary>고점 소진 거래량 배수 override.</summary>
    public double? TopVolumeRatio { get; set; }
    /// <summary>🔊 거래량 급증 RVOL 배수(직전 20봉 평균比) override. 높일수록 더 확실한 급증만 알림.</summary>
    public double? VolumeSurgeRvol { get; set; }

    /// <summary>바닥/고점 파라미터 중 하나라도 종목 전용값이 있으면 true(그리드 "개별" 표시용).</summary>
    public bool HasSignalOverride =>
        BottomRsiMax.HasValue || BottomVolumeRatio.HasValue || BottomWalkMaxTouches.HasValue ||
        BottomMinPercentB.HasValue || BottomGcMinRisePct.HasValue || BottomGcStrongPct.HasValue ||
        TopRsiMin.HasValue || TopVolumeRatio.HasValue || VolumeSurgeRvol.HasValue;

    /// <summary>그리드 표시용: 전용 조건이 있으면 요약, 없으면 "전역".</summary>
    public string RulesLabel => Rules.Count > 0 ? TrendRule.Summary(Rules) : "전역";

    public string MarketLabel => IsIndex ? "지수" : Market == MarketKind.US ? "미국" : "국내";
    public string SourceLabel => IsIndex ? "KIS" : Source switch
    {
        WatchSource.Naver => "네이버",
        WatchSource.Yahoo => "Yahoo",
        WatchSource.Kis => "KIS",
        WatchSource.Finnhub => "Finnhub",
        WatchSource.Alpaca => "Alpaca",
        WatchSource.Databento => "Databento",
        _ => Source.ToString()
    };

    public override string ToString() => string.IsNullOrEmpty(Name) ? Symbol : $"{Symbol}  {Name}";
}
