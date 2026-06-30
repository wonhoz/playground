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

    public string MarketLabel => Market == MarketKind.US ? "미국" : "국내";
    public string SourceLabel => Source switch
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
