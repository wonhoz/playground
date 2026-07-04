using System.Text.Json.Serialization;

namespace Stock.Catch.Models;

/// <summary>매매 구분.</summary>
public enum TradeSide { Buy, Sell }

/// <summary>단일 매매 기록(롯). 평단·실현손익은 이 기록들로부터 계산된다.</summary>
public sealed class Trade
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TradeSide Side { get; set; } = TradeSide.Buy;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Note { get; set; } = string.Empty;

    [JsonIgnore] public decimal Amount => Price * Quantity;
}

/// <summary>
/// 종목별 보유 현황(매매 기록을 이동평균법으로 누적한 결과).
/// Quantity=0이면 청산(전량 매도)된 종목이며 Realized만 남는다.
/// </summary>
public sealed record Holding(
    string Code,
    string Name,
    int Quantity,        // 현재 보유 수량
    decimal AvgPrice,    // 평단(이동평균 매입가)
    decimal Invested,    // 매입금액(Quantity × AvgPrice)
    decimal Realized);   // 누적 실현손익

/// <summary>자산 포트폴리오(매매 기록 모음). JSON 세이브 파일로 영속화.</summary>
public sealed class Portfolio
{
    public List<Trade> Trades { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
