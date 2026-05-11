using Quant.Lab.Core.Data;

namespace Quant.Lab.Core.Strategies;

public enum OrderSide { Buy, Sell }

public sealed class StrategyContext
{
    private readonly List<PendingOrder> _orders;

    internal StrategyContext(IReadOnlyList<OhlcBar> bars, decimal cash, List<PendingOrder> orders)
    {
        Bars = bars;
        Cash = cash;
        _orders = orders;
    }

    public IReadOnlyList<OhlcBar> Bars { get; }
    public int Index { get; internal set; }
    public OhlcBar Current => Bars[Index];
    public decimal Cash { get; internal set; }
    public long Position { get; internal set; }
    public decimal Equity => Cash + Position * Current.Close;

    public void BuyMarket(long quantity)
    {
        if (quantity <= 0) return;
        _orders.Add(new PendingOrder(OrderSide.Buy, quantity));
    }

    public void SellMarket(long quantity)
    {
        if (quantity <= 0 || Position <= 0) return;
        _orders.Add(new PendingOrder(OrderSide.Sell, Math.Min(quantity, Position)));
    }

    public void BuyAll() => BuyMarket(MaxBuyableQuantity());
    public void SellAll() => SellMarket(Position);

    public long MaxBuyableQuantity()
    {
        var nextOpen = Index + 1 < Bars.Count ? Bars[Index + 1].Open : Current.Close;
        return nextOpen <= 0 ? 0 : (long)(Cash / nextOpen);
    }
}

internal sealed record PendingOrder(OrderSide Side, long Quantity);
