namespace Stock.Watch.Models;

/// <summary>
/// 단일 봉(일/분) OHLCV 데이터. KIS API 응답 및 차트/지표 계산의 기본 단위.
/// </summary>
public sealed record Candle(
    DateTime Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume)
{
    /// <summary>양봉(종가 ≥ 시가) 여부.</summary>
    public bool IsBullish => Close >= Open;
}
