using Quant.Lab.Core.Engine;

namespace Quant.Lab.Core.Reporting;

public sealed record PerformanceMetrics(
    decimal InitialCash,
    decimal FinalEquity,
    double TotalReturn,
    double Cagr,
    double MaxDrawdown,
    double SharpeRatio,
    int TradeCount,
    int WinCount,
    int LossCount,
    double WinRate,
    double AverageWinReturn,
    double AverageLossReturn);

public static class MetricsCalculator
{
    public static PerformanceMetrics Calculate(
        IReadOnlyList<EquityPoint> equity,
        IReadOnlyList<Trade> trades,
        decimal initialCash)
    {
        if (equity.Count == 0)
            return new PerformanceMetrics(initialCash, initialCash, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var finalEquity = equity[^1].Equity;
        var totalReturn = (double)((finalEquity - initialCash) / initialCash);

        var days = equity[^1].Date.DayNumber - equity[0].Date.DayNumber;
        var years = days > 0 ? days / 365.25 : 1.0 / 365.25;
        var cagr = years > 0 && initialCash > 0
            ? Math.Pow((double)(finalEquity / initialCash), 1.0 / years) - 1
            : 0;

        decimal peak = equity[0].Equity;
        double maxDd = 0;
        foreach (var p in equity)
        {
            if (p.Equity > peak) peak = p.Equity;
            var dd = peak == 0 ? 0 : (double)((p.Equity - peak) / peak);
            if (dd < maxDd) maxDd = dd;
        }

        var dailyReturns = new List<double>(equity.Count);
        for (int i = 1; i < equity.Count; i++)
        {
            var prev = (double)equity[i - 1].Equity;
            var curr = (double)equity[i].Equity;
            if (prev > 0 && curr > 0)
                dailyReturns.Add(Math.Log(curr / prev));
        }

        double sharpe = 0;
        if (dailyReturns.Count > 1)
        {
            var mean = dailyReturns.Average();
            var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / (dailyReturns.Count - 1);
            var std = Math.Sqrt(variance);
            if (std > 0) sharpe = mean / std * Math.Sqrt(252);
        }

        var wins = trades.Where(t => t.Return > 0).ToList();
        var losses = trades.Where(t => t.Return <= 0).ToList();

        return new PerformanceMetrics(
            initialCash,
            finalEquity,
            totalReturn,
            cagr,
            maxDd,
            sharpe,
            trades.Count,
            wins.Count,
            losses.Count,
            trades.Count == 0 ? 0 : (double)wins.Count / trades.Count,
            wins.Count == 0 ? 0 : wins.Average(t => t.Return),
            losses.Count == 0 ? 0 : losses.Average(t => t.Return));
    }
}
