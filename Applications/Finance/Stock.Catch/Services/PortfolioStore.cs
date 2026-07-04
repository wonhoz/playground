using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 자산 포트폴리오(매매 기록)의 JSON 세이브 파일 영속화 + 보유/평단/실현손익 계산.
/// <para>저장 경로는 <see cref="AppConfig.PortfolioPath"/>(비어 있으면 문서\StockCatch\portfolio.json).
/// Dropbox 등 동기화 폴더를 지정하면 어느 PC에서든 같은 파일을 공유한다.</para>
/// </summary>
public sealed class PortfolioStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>설정의 경로(빈 값이면 문서 폴더 기본 경로 · 구 StockFetch 파일이 있으면 그대로 사용).</summary>
    public static string ResolvePath(AppConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.PortfolioPath)) return cfg.PortfolioPath;
        // Stock.Fetch → Stock.Catch 마이그레이션: 새 기본 파일이 없고 구 파일이 있으면 구 경로 유지(자산 보존).
        if (!File.Exists(DefaultPath) && File.Exists(LegacyDefaultPath)) return LegacyDefaultPath;
        return DefaultPath;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "StockCatch", "portfolio.json");

    /// <summary>구 앱 이름(StockFetch) 시절 기본 경로.</summary>
    private static string LegacyDefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "StockFetch", "portfolio.json");

    public static Portfolio Load(AppConfig cfg)
    {
        string path = ResolvePath(cfg);
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var pf = JsonSerializer.Deserialize<Portfolio>(json, JsonOpts);
                if (pf != null) return pf;
            }
        }
        catch { /* 손상/접근 불가 시 빈 포트폴리오로 폴백 */ }
        return new Portfolio();
    }

    public static void Save(AppConfig cfg, Portfolio pf)
    {
        string path = ResolvePath(cfg);
        pf.UpdatedAt = DateTime.Now;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(pf, JsonOpts));
    }

    /// <summary>
    /// 매매 기록을 종목별로 이동평균법 누적해 보유 현황을 산출한다.
    /// 매수: 평단=(기존수량×평단 + 매수금액)/(총수량). 매도: 실현손익+=수량×(매도가−평단), 평단 유지.
    /// </summary>
    public static List<Holding> Holdings(Portfolio pf)
    {
        var result = new List<Holding>();
        foreach (var grp in pf.Trades.GroupBy(t => t.Code))
        {
            decimal qty = 0, avg = 0, realized = 0;
            string name = grp.Key;
            foreach (var t in grp.OrderBy(t => t.Date).ThenBy(t => t.Side == TradeSide.Buy ? 0 : 1))
            {
                if (!string.IsNullOrWhiteSpace(t.Name)) name = t.Name;
                if (t.Side == TradeSide.Buy)
                {
                    decimal newQty = qty + t.Quantity;
                    if (newQty > 0) avg = (qty * avg + t.Price * t.Quantity) / newQty;
                    qty = newQty;
                }
                else // Sell
                {
                    int s = (int)Math.Min(t.Quantity, qty);
                    realized += s * (t.Price - avg);
                    qty -= s;
                    if (qty == 0) avg = 0;
                }
            }
            result.Add(new Holding(grp.Key, name, (int)qty, avg, qty * avg, realized));
        }
        return result.OrderByDescending(h => h.Quantity > 0).ThenByDescending(h => h.Invested).ToList();
    }

    /// <summary>특정 종목의 현재 보유(없으면 null).</summary>
    public static Holding? HoldingOf(Portfolio pf, string code) =>
        Holdings(pf).FirstOrDefault(h => h.Code == code && h.Quantity > 0);
}
