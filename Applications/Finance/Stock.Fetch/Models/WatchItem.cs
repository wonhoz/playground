namespace Stock.Fetch.Models;

/// <summary>관심 종목의 시장 구분.</summary>
public enum MarketKind { KR, US }

/// <summary>
/// 관심 종목 시세 소스. KR=Naver(무인증·지연)/Kis(실시간),
/// US=Yahoo(지연)/Finnhub(실시간·무료키)/Alpaca(실시간 IEX·무료키)/Kis(준실시간).
/// </summary>
public enum WatchSource { Naver, Yahoo, Kis, Finnhub, Alpaca }

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
        _ => Source.ToString()
    };

    public override string ToString() => string.IsNullOrEmpty(Name) ? Symbol : $"{Symbol}  {Name}";
}
