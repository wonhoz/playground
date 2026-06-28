namespace Stock.Fetch.Models;

/// <summary>
/// 단일 일봉 OHLCV 데이터. 모든 데이터 소스(KIS·네이버·KRX·Yahoo)가 이 형태로 정규화해 반환한다.
/// </summary>
public sealed record Candle(
    DateTime Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume)
{
    /// <summary>양봉(종가 ≥ 시가) 여부.</summary>
    public bool IsBullish => Close >= Open;
}
