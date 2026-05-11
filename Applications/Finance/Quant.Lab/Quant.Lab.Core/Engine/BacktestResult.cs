using Quant.Lab.Core.Reporting;

namespace Quant.Lab.Core.Engine;

public sealed record BacktestResult(
    string StrategyName,
    BacktestConfig Config,
    IReadOnlyList<EquityPoint> Equity,
    IReadOnlyList<Trade> Trades,
    PerformanceMetrics Metrics);
