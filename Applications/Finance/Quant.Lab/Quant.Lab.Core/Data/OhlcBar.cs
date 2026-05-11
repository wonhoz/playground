namespace Quant.Lab.Core.Data;

public sealed record OhlcBar(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
