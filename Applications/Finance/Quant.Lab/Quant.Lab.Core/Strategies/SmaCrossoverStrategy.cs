using Quant.Lab.Core.Data;

namespace Quant.Lab.Core.Strategies;

public sealed class SmaCrossoverStrategy : IStrategy
{
    private readonly int _shortPeriod;
    private readonly int _longPeriod;

    public SmaCrossoverStrategy(int shortPeriod = 5, int longPeriod = 20)
    {
        if (shortPeriod <= 0 || longPeriod <= 0)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod), "기간은 양수여야 합니다.");
        if (shortPeriod >= longPeriod)
            throw new ArgumentException("단기 기간은 장기 기간보다 짧아야 합니다.", nameof(shortPeriod));
        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;
    }

    public string Name => $"SMA Crossover ({_shortPeriod}/{_longPeriod})";

    public void OnBar(StrategyContext ctx)
    {
        if (ctx.Index < _longPeriod) return;

        var prevShort = Sma(ctx.Bars, ctx.Index - 1, _shortPeriod);
        var prevLong = Sma(ctx.Bars, ctx.Index - 1, _longPeriod);
        var currShort = Sma(ctx.Bars, ctx.Index, _shortPeriod);
        var currLong = Sma(ctx.Bars, ctx.Index, _longPeriod);

        bool goldenCross = prevShort <= prevLong && currShort > currLong;
        bool deadCross = prevShort >= prevLong && currShort < currLong;

        if (goldenCross && ctx.Position == 0)
            ctx.BuyAll();
        else if (deadCross && ctx.Position > 0)
            ctx.SellAll();
    }

    private static decimal Sma(IReadOnlyList<OhlcBar> bars, int index, int period)
    {
        decimal sum = 0;
        for (int i = index - period + 1; i <= index; i++)
            sum += bars[i].Close;
        return sum / period;
    }
}
