namespace StockRush.Services;

/// <summary>
/// 가상 계좌. 수수료 0.015% (매수·매도), 거래세 0.2% (매도).
/// </summary>
public class Account
{
    public const long InitialCash = 10_000_000;
    public const double CommissionRate = 0.00015;
    public const double TaxRate = 0.0020;

    public long Cash { get; private set; } = InitialCash;
    public long RealizedPnlToday { get; set; }
    public ObservableCollection<Position> Positions { get; } = new();
    public ObservableCollection<Order> OpenOrders { get; } = new();
    public ObservableCollection<TradeRecord> Trades { get; } = new();

    /// <summary>당일 체결 전체 (Trades는 100건 캡 — 일일 통계용 별도 보관)</summary>
    public List<TradeRecord> TradesToday { get; } = new();

    /// <summary>다음 날 개장 전 일일 집계 초기화</summary>
    public void ResetDay()
    {
        RealizedPnlToday = 0;
        TradesToday.Clear();
    }

    public event Action<TradeRecord>? TradeExecuted;

    public Position? GetPosition(string code) => Positions.FirstOrDefault(p => p.Code == code);

    public long Equity(IEnumerable<Stock> stocks)
    {
        var total = Cash;
        foreach (var p in Positions)
        {
            var s = stocks.FirstOrDefault(x => x.Code == p.Code);
            total += (s?.Price ?? p.CurrentPrice) * p.Qty;
        }
        return total;
    }

    public long MaxBuyQty(long price)
    {
        if (price <= 0) return 0;
        return (long)(Cash / (price * (1 + CommissionRate)));
    }

    /// <summary>매수 체결. 성공 시 null, 실패 시 사유 반환.</summary>
    public string? ExecuteBuy(Stock stock, long price, long qty, int day, TimeSpan time)
    {
        if (qty <= 0) return "수량을 입력하세요.";
        var cost = price * qty;
        var fee = (long)Math.Round(cost * CommissionRate);
        if (cost + fee > Cash) return "예수금이 부족합니다.";

        Cash -= cost + fee;
        var pos = GetPosition(stock.Code);
        if (pos == null)
        {
            pos = new Position { Code = stock.Code, Name = stock.Name, Qty = qty, AvgPrice = price, CurrentPrice = stock.Price };
            Positions.Add(pos);
        }
        else
        {
            pos.AvgPrice = (pos.AvgPrice * pos.Qty + cost) / (pos.Qty + qty);
            pos.Qty += qty;
        }

        Record(stock, OrderSide.매수, price, qty, 0, day, time);
        return null;
    }

    /// <summary>매도 체결. 성공 시 null, 실패 시 사유 반환.</summary>
    public string? ExecuteSell(Stock stock, long price, long qty, int day, TimeSpan time)
    {
        if (qty <= 0) return "수량을 입력하세요.";
        var pos = GetPosition(stock.Code);
        if (pos == null || pos.Qty < qty) return "보유 수량이 부족합니다.";

        var proceeds = price * qty;
        var fee = (long)Math.Round(proceeds * CommissionRate);
        var tax = (long)Math.Round(proceeds * TaxRate);
        Cash += proceeds - fee - tax;

        var realized = (price - pos.AvgPrice) * qty - fee - tax;
        RealizedPnlToday += realized;

        pos.Qty -= qty;
        if (pos.Qty == 0) Positions.Remove(pos);

        Record(stock, OrderSide.매도, price, qty, realized, day, time);
        return null;
    }

    private void Record(Stock stock, OrderSide side, long price, long qty, long realized, int day, TimeSpan time)
    {
        var rec = new TradeRecord
        {
            Day = day, Time = time, Code = stock.Code, Name = stock.Name,
            Side = side, Price = price, Qty = qty, RealizedPnl = realized
        };
        Trades.Insert(0, rec);
        while (Trades.Count > 100) Trades.RemoveAt(Trades.Count - 1);
        TradesToday.Add(rec);
        TradeExecuted?.Invoke(rec);
    }

    /// <summary>지정가 주문 등록</summary>
    public string? PlaceLimitOrder(Stock stock, OrderSide side, long limitPrice, long qty)
    {
        if (qty <= 0) return "수량을 입력하세요.";
        if (limitPrice <= 0) return "가격을 입력하세요.";
        if (side == OrderSide.매수)
        {
            var reserve = (long)(limitPrice * qty * (1 + CommissionRate));
            var pending = OpenOrders.Where(o => o.Side == OrderSide.매수)
                                    .Sum(o => (long)(o.LimitPrice * o.Qty * (1 + CommissionRate)));
            if (reserve + pending > Cash) return "예수금이 부족합니다. (대기 주문 포함)";
        }
        else
        {
            var pos = GetPosition(stock.Code);
            var pendingSell = OpenOrders.Where(o => o.Side == OrderSide.매도 && o.Code == stock.Code).Sum(o => o.Qty);
            if (pos == null || pos.Qty < qty + pendingSell) return "보유 수량이 부족합니다. (대기 주문 포함)";
        }
        OpenOrders.Add(new Order { Code = stock.Code, Name = stock.Name, Side = side, Type = OrderType.지정가, LimitPrice = limitPrice, Qty = qty });
        return null;
    }

    public void CancelOrder(Order order)
    {
        order.Status = OrderStatus.취소;
        OpenOrders.Remove(order);
    }

    /// <summary>매 틱: 지정가 주문 체결 검사. 체결된 주문 목록 반환.</summary>
    public List<Order> MatchLimitOrders(MarketEngine engine)
    {
        var filled = new List<Order>();
        foreach (var o in OpenOrders.ToList())
        {
            var s = engine.Find(o.Code);
            if (s == null || s.HaltTicks > 0) continue;

            var triggered = o.Side == OrderSide.매수 ? s.Price <= o.LimitPrice : s.Price >= o.LimitPrice;
            if (!triggered) continue;

            var err = o.Side == OrderSide.매수
                ? ExecuteBuy(s, o.LimitPrice, o.Qty, engine.Day, engine.MarketTime)
                : ExecuteSell(s, o.LimitPrice, o.Qty, engine.Day, engine.MarketTime);

            o.Status = err == null ? OrderStatus.체결 : OrderStatus.취소;
            OpenOrders.Remove(o);
            if (err == null) filled.Add(o);
        }
        return filled;
    }
}
