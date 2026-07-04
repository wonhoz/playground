namespace Stock.Catch.Models;

/// <summary>익절가 제안 한 가지(방식별).</summary>
public sealed record SellTarget(
    string Name,            // 방식 이름
    string Note,            // 산출 근거(앵커·계수 등)
    decimal Price,          // 익절가
    decimal ReturnPct,      // 익절가/평단 − 1
    decimal ReachProb);     // 추정 도달확률(당일 고가 ≥ 익절가, 최근 분포 기준)

/// <summary>
/// stock-update 스킬의 "저점 분할 지정가 매수 + 익절/손절" 래더 계산 결과(단일 종목).
/// 수량은 1/1/1/1 고정, 가격은 100원 단위(MROUND). 익절가는 4방식으로 함께 제안한다.
/// 공격성(보수↔공격) 슬라이더·추세 자동 반영으로 매수 깊이·익절 강도가 가변된다.
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
    int SellOffset,            // 고가변화율 분위(%) — 방식 1·3 공통
    decimal Atr,               // 진단(평균 True Range)
    SellTarget[] SellTargets,  // 4개
    // ── 진단 ──
    decimal SigmaDown,         // 하방 변동성(%)
    // ── 공격성·추세·확률 ──
    double BuyAggressiveness,  // 0(보수)~1(공격) — 실제 적용값
    double SellStrength,       // 0(보수)~1(공격) — 실제 적용값
    double[] FillProbs,        // 매수 호가별 추정 체결확률(0~1)
    double TrendScore,         // −1(하락)~+1(상승)
    string TrendLabel,         // 상승/중립/하락 + 구성 요약
    bool TrendApplied,         // 추세 자동 반영 여부
    // ── 보유 평단 반영(포트폴리오 연동) ──
    int HoldingQty,            // 현재 보유 수량(0이면 미보유)
    decimal HoldingAvg,        // 현재 보유 평단
    decimal CombinedAvg);      // 보유+신규4주 합산 평단(손절·익절 기준). 미보유면 신규 평단과 동일
