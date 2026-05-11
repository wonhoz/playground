namespace Quant.Lab.Core.Engine;

public sealed record BacktestConfig(
    decimal InitialCash = 10_000_000m,
    decimal CommissionRate = 0.00015m,
    decimal TaxRate = 0.0023m,
    decimal SlippageRate = 0.0005m,
    bool LiquidateAtEnd = true);
