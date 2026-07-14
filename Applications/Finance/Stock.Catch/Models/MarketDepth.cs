namespace Stock.Catch.Models;

/// <summary>호가 1단(가격·잔량).</summary>
public sealed record AskBid(decimal Price, long Qty);

/// <summary>
/// 실시간 호가창(10단) — inquire-asking-price-exp-ccn(FHKST01010200).
/// <see cref="Asks"/>는 1호가(최우선 매도)부터, <see cref="Bids"/>도 1호가(최우선 매수)부터.
/// </summary>
public sealed record MarketDepth(
    IReadOnlyList<AskBid> Asks,
    IReadOnlyList<AskBid> Bids,
    long TotalAsk,
    long TotalBid,
    decimal Price)
{
    /// <summary>매수/매도 잔량 불균형(양수=매수 우위) 비율 %. total 기준.</summary>
    public double BidStrength => TotalAsk + TotalBid > 0 ? (double)(TotalBid - TotalAsk) / (TotalBid + TotalAsk) * 100 : 0;
}

/// <summary>
/// 실시간 수급 스냅샷 — 외국인/기관/개인/프로그램 순매수·외국인 소진율·체결강도.
/// inquire-price(외인·프로그램·소진율) + inquire-investor(기관·개인) + inquire-ccnl(체결강도) 조합.
/// 순매수 수량은 당일 누적(외인·프로그램은 실시간 근접, 기관·개인은 투자자 집계 기준).
/// </summary>
public sealed record SupplyDemand(
    decimal Price,
    decimal ChangeRate,
    long ForeignNet,        // 외국인 순매수 수량
    long InstitutionNet,    // 기관 순매수 수량
    long PersonNet,         // 개인 순매수 수량
    long ProgramNet,        // 프로그램 순매수 수량
    double ForeignExhaust,  // 외국인 소진율 %
    double ExecStrength);   // 체결강도(당일)
