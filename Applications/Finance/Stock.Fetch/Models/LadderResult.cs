namespace Stock.Fetch.Models;

/// <summary>익절가 제안 한 가지(방식별).</summary>
public sealed record SellTarget(
    string Name,            // 방식 이름
    string Note,            // 산출 근거(앵커·계수 등)
    decimal Price,          // 익절가
    decimal ReturnPct);     // 익절가/평단 − 1

/// <summary>
/// stock-update 스킬의 "저점 분할 지정가 매수 + 익절/손절" 래더 계산 결과(단일 종목).
/// 수량은 1/1/1/1 고정, 가격은 100원 단위(MROUND). 익절가는 4방식으로 함께 제안한다.
/// </summary>
public sealed record LadderResult(
    string Code,
    string Name,
    int TradingDays,
    // ── 기준값(마지막 거래일) ──
    decimal PrevLow,
    decimal PrevHigh,
    decimal PrevClose,
    decimal GapCancelLine,     // 정규종가 −5%
    // ── 매수 래더 ──
    int[] BuyOffsets,          // 4개(%)
    decimal[] BuyPrices,       // 4개
    decimal AvgPrice,          // 평단
    decimal TotalAmount,       // 전량금액(4주 합)
    // ── 리스크 ──
    decimal StopPrice,         // 평단 −8%
    decimal StopLoss,          // (평단−손절가)×4
    // ── 익절(4방식 제안) ──
    int SellOffset,            // 고가변화율 P20(%) — 방식 1·3 공통
    decimal Atr,               // 진단(평균 True Range)
    SellTarget[] SellTargets,  // 4개
    // ── 진단 ──
    decimal SigmaDown);        // 하방 변동성(%)
