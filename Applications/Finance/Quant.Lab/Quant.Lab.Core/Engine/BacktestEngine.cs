using Quant.Lab.Core.Data;
using Quant.Lab.Core.Reporting;
using Quant.Lab.Core.Strategies;

namespace Quant.Lab.Core.Engine;

public sealed class BacktestEngine
{
    private readonly BacktestConfig _config;

    public BacktestEngine(BacktestConfig config) => _config = config;

    public BacktestResult Run(IReadOnlyList<OhlcBar> bars, IStrategy strategy)
    {
        if (bars.Count < 2)
            throw new InvalidDataException("바 데이터가 최소 2개 이상 필요합니다.");

        var pending = new List<PendingOrder>();
        var ctx = new StrategyContext(bars, _config.InitialCash, pending);
        var trades = new List<Trade>();
        var equity = new List<EquityPoint>(bars.Count);
        OpenTrade? open = null;

        for (int i = 0; i < bars.Count; i++)
        {
            ctx.Index = i;

            // 1) 직전 봉에서 발생한 시그널을 현재 봉 시가에 체결 (look-ahead bias 방지)
            if (pending.Count > 0)
            {
                foreach (var o in pending)
                    open = Execute(o, bars[i], ctx, trades, open);
                pending.Clear();
            }

            // 2) 전략 호출 — 종가 기준 시그널은 다음 봉 시가에 체결
            strategy.OnBar(ctx);

            // 3) 자산 곡선 (현재 봉 종가 평가)
            equity.Add(new EquityPoint(bars[i].Date, ctx.Cash + ctx.Position * bars[i].Close));
        }

        // 마지막 보유분 강제 청산
        if (_config.LiquidateAtEnd && ctx.Position > 0 && open != null)
        {
            var last = bars[^1];
            var fill = ApplySlippage(last.Close, OrderSide.Sell);
            var proceeds = fill * ctx.Position;
            var fee = proceeds * (_config.CommissionRate + _config.TaxRate);
            ctx.Cash += proceeds - fee;

            var pnl = (fill - open.AvgPrice) * ctx.Position - open.EntryCommission - fee;
            var ret = (double)((fill - open.AvgPrice) / open.AvgPrice);
            trades.Add(new Trade(open.EntryDate, last.Date, open.AvgPrice, fill, ctx.Position, pnl, ret));

            ctx.Position = 0;
            equity[^1] = new EquityPoint(last.Date, ctx.Cash);
        }

        var metrics = MetricsCalculator.Calculate(equity, trades, _config.InitialCash);
        return new BacktestResult(strategy.Name, _config, equity, trades, metrics);
    }

    private OpenTrade? Execute(PendingOrder order, OhlcBar fillBar, StrategyContext ctx, List<Trade> trades, OpenTrade? open)
    {
        var fillPrice = ApplySlippage(fillBar.Open, order.Side);

        if (order.Side == OrderSide.Buy)
        {
            var cost = fillPrice * order.Quantity;
            var fee = cost * _config.CommissionRate;
            if (ctx.Cash < cost + fee) return open;

            ctx.Cash -= cost + fee;
            ctx.Position += order.Quantity;

            return open == null
                ? new OpenTrade(fillBar.Date, fillPrice, order.Quantity, fee)
                : open with
                {
                    AvgPrice = (open.AvgPrice * open.Quantity + fillPrice * order.Quantity) / (open.Quantity + order.Quantity),
                    Quantity = open.Quantity + order.Quantity,
                    EntryCommission = open.EntryCommission + fee
                };
        }

        // Sell
        var qty = Math.Min(order.Quantity, ctx.Position);
        if (qty <= 0 || open == null) return open;

        var sellProceeds = fillPrice * qty;
        var sellFee = sellProceeds * (_config.CommissionRate + _config.TaxRate);
        ctx.Cash += sellProceeds - sellFee;
        ctx.Position -= qty;

        var tradePnl = (fillPrice - open.AvgPrice) * qty - open.EntryCommission - sellFee;
        var tradeRet = (double)((fillPrice - open.AvgPrice) / open.AvgPrice);
        trades.Add(new Trade(open.EntryDate, fillBar.Date, open.AvgPrice, fillPrice, qty, tradePnl, tradeRet));

        return ctx.Position == 0 ? null : open;
    }

    private decimal ApplySlippage(decimal price, OrderSide side)
        => side == OrderSide.Buy
            ? price * (1 + _config.SlippageRate)
            : price * (1 - _config.SlippageRate);

    private sealed record OpenTrade(DateOnly EntryDate, decimal AvgPrice, long Quantity, decimal EntryCommission);
}
