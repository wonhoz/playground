namespace Stock.Catch.Models;

/// <summary>반등 방향. BottomUp=하락→상승(바닥반등), TopDown=상승→하락(천정반전).</summary>
public enum ReversalDir { BottomUp, TopDown }

/// <summary>
/// 반등(반전) 확률 추정 결과. 여러 지표(RSI·볼린저·이격도·캔들·거래량)를 가중 결합한
/// <b>휴리스틱 추정치</b>이며 통계적으로 검증된 확률이 아니다.
/// </summary>
public sealed record ReversalEstimate(ReversalDir Dir, double Probability, string Detail, bool Calibrated = false)
{
    public string DirText => Dir == ReversalDir.BottomUp ? "하락→상승 반등" : "상승→하락 반전";
    public string ProbText => $"{Probability:P0}";
    /// <summary>확률 성격 표기: 보정됨=과거적중, 미보정=지표 휴리스틱.</summary>
    public string BasisText => Calibrated ? "과거적중" : "지표 휴리스틱";
}
