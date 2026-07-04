namespace Stock.Catch.Models;

/// <summary>
/// 반등 raw 점수(0~1)를 과거 백테스트 적중률로 보정하는 신뢰도 곡선(10구간). raw를 넣으면
/// 해당 구간의 과거 적중률을 돌려준다. 통계적 근사이며 미래 수익 보장이 아니다.
/// </summary>
public sealed class ReversalCalibration
{
    /// <summary>구간 [0,0.1)…[0.9,1.0]의 보정 적중률(0~1). 단조 비감소.</summary>
    public double[] BinRates { get; set; } = new double[10];
    public int[] BinCounts { get; set; } = new int[10];
    public int TotalSamples { get; set; }
    public double BaseRate { get; set; }
    public int HorizonDays { get; set; } = 5;
    public double ThresholdPct { get; set; } = 2.0;   // %
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>raw 점수 → 보정 적중률.</summary>
    public double Apply(double raw)
    {
        if (BinRates.Length == 0) return raw;
        int idx = Math.Clamp((int)(raw * 10), 0, BinRates.Length - 1);
        double v = BinRates[idx];
        return double.IsNaN(v) || v < 0 ? raw : Math.Clamp(v, 0, 1);
    }

    public string Summary =>
        TotalSamples > 0
            ? $"표본 {TotalSamples:N0} · 전체적중 {BaseRate:P0} · {HorizonDays}일/{ThresholdPct:0.#}% · {CreatedAt:yyyy-MM-dd}"
            : "미학습";
}
