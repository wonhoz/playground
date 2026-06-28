namespace Stock.Fetch.Models;

/// <summary>데이터 소스 종류.</summary>
public enum SourceKind
{
    Naver,
    Yahoo,
    Daum,
    Kis,
}

/// <summary>
/// 한 종목의 조회 결과. 종목 메타(코드·이름·시장)와 일봉 시계열을 함께 담는다.
/// 모든 소스가 이 형태로 정규화해 반환한다.
/// </summary>
public sealed record StockSeries(
    string Code,
    string Name,
    string Market,
    SourceKind Source,
    IReadOnlyList<Candle> Candles)
{
    public string SourceLabel => Source switch
    {
        SourceKind.Naver => "네이버 금융",
        SourceKind.Yahoo => "Yahoo Finance",
        SourceKind.Daum => "다음 금융",
        SourceKind.Kis => "한국투자증권 OpenAPI",
        _ => Source.ToString()
    };
}
