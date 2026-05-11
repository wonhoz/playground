using System.Globalization;
using Quant.Lab.Core.Data;
using Quant.Lab.Core.Engine;
using Quant.Lab.Core.Reporting;
using Quant.Lab.Core.Strategies;

namespace Quant.Lab.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
            {
                PrintHelp();
                return 0;
            }

            var opts = ParseArgs(args);

            Console.WriteLine($"[Quant.Lab] CSV: {opts.CsvPath}");
            var bars = new CsvDataLoader(opts.CsvPath).Load();
            Console.WriteLine($"[Quant.Lab] Bars loaded: {bars.Count} ({bars[0].Date:yyyy-MM-dd} ~ {bars[^1].Date:yyyy-MM-dd})");

            IStrategy strategy = opts.Strategy.ToLowerInvariant() switch
            {
                "sma" => new SmaCrossoverStrategy(opts.ShortPeriod, opts.LongPeriod),
                _ => throw new ArgumentException($"알 수 없는 전략: {opts.Strategy}. 지원: sma")
            };
            Console.WriteLine($"[Quant.Lab] Strategy: {strategy.Name}");

            var config = new BacktestConfig(
                InitialCash: opts.InitialCash,
                CommissionRate: opts.Commission,
                TaxRate: opts.Tax,
                SlippageRate: opts.Slippage);

            var engine = new BacktestEngine(config);
            var result = engine.Run(bars, strategy);

            PrintSummary(result);

            if (!string.IsNullOrEmpty(opts.ReportPath))
            {
                HtmlReporter.Save(result, opts.ReportPath);
                Console.WriteLine($"[Quant.Lab] HTML 리포트: {Path.GetFullPath(opts.ReportPath)}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] {ex.Message}");
            return 1;
        }
    }

    private sealed record CliOptions(
        string CsvPath,
        string Strategy,
        int ShortPeriod,
        int LongPeriod,
        decimal InitialCash,
        decimal Commission,
        decimal Tax,
        decimal Slippage,
        string? ReportPath);

    private static CliOptions ParseArgs(string[] args)
    {
        string? csv = null;
        string strategy = "sma";
        int shortP = 5, longP = 20;
        decimal cash = 10_000_000m;
        decimal commission = 0.00015m;
        decimal tax = 0.0023m;
        decimal slippage = 0.0005m;
        string? report = null;

        foreach (var raw in args)
        {
            var (key, value) = SplitArg(raw);
            switch (key)
            {
                case "--csv":      csv = value; break;
                case "--strategy": strategy = value ?? strategy; break;
                case "--short":    shortP = int.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--long":     longP = int.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--cash":     cash = decimal.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--commission": commission = decimal.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--tax":      tax = decimal.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--slippage": slippage = decimal.Parse(value!, CultureInfo.InvariantCulture); break;
                case "--report":   report = value; break;
                default:
                    throw new ArgumentException($"알 수 없는 인자: {raw}");
            }
        }

        if (string.IsNullOrEmpty(csv))
            throw new ArgumentException("--csv=<경로> 인자가 필요합니다. --help 참조.");

        return new CliOptions(csv, strategy, shortP, longP, cash, commission, tax, slippage, report);
    }

    private static (string key, string? value) SplitArg(string arg)
    {
        var eq = arg.IndexOf('=');
        return eq < 0 ? (arg, null) : (arg[..eq], arg[(eq + 1)..]);
    }

    private static void PrintSummary(BacktestResult r)
    {
        var m = r.Metrics;
        Console.WriteLine();
        Console.WriteLine("=== Backtest Summary ===");
        Console.WriteLine($"  Strategy       : {r.StrategyName}");
        Console.WriteLine($"  Period         : {r.Equity[0].Date:yyyy-MM-dd} ~ {r.Equity[^1].Date:yyyy-MM-dd} ({r.Equity.Count} bars)");
        Console.WriteLine($"  Initial Cash   : {m.InitialCash:N0} KRW");
        Console.WriteLine($"  Final Equity   : {m.FinalEquity:N0} KRW");
        Console.WriteLine($"  Total Return   : {m.TotalReturn * 100:F2}%");
        Console.WriteLine($"  CAGR           : {m.Cagr * 100:F2}%");
        Console.WriteLine($"  Max Drawdown   : {m.MaxDrawdown * 100:F2}%");
        Console.WriteLine($"  Sharpe Ratio   : {m.SharpeRatio:F2}");
        Console.WriteLine($"  Trades         : {m.TradeCount} (W:{m.WinCount} / L:{m.LossCount})");
        Console.WriteLine($"  Win Rate       : {m.WinRate * 100:F2}%");
        Console.WriteLine($"  Avg Win        : {m.AverageWinReturn * 100:F2}%");
        Console.WriteLine($"  Avg Loss       : {m.AverageLossReturn * 100:F2}%");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Quant.Lab — KRX 일봉 백테스트 CLI

            사용법:
              Quant.Lab --csv=<file> [옵션]

            옵션:
              --csv=<path>          (필수) OHLCV CSV 경로
              --strategy=<name>     전략 (기본 sma)
              --short=<int>         SMA 단기 기간 (기본 5)
              --long=<int>          SMA 장기 기간 (기본 20)
              --cash=<decimal>      초기 자본 (기본 10000000)
              --commission=<rate>   매수/매도 수수료율 (기본 0.00015 = 0.015%)
              --tax=<rate>          매도 거래세율 (기본 0.0023 = 0.23%)
              --slippage=<rate>     슬리피지율 (기본 0.0005 = 0.05%)
              --report=<path>       HTML 리포트 출력 경로 (옵션)
              -h, --help            도움말

            예시:
              Quant.Lab --csv=samples/005930_sample.csv --short=5 --long=20 --report=report.html
            """);
    }
}
