namespace Quant.Lab.Core.Engine;

public sealed record Trade(
    DateOnly EntryDate,
    DateOnly ExitDate,
    decimal EntryPrice,
    decimal ExitPrice,
    long Quantity,
    decimal Pnl,
    double Return);
