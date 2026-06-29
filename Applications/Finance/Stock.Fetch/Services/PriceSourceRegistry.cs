using System.Net.Http;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 4개 데이터 소스 인스턴스를 생성·보관하고 공유 <see cref="HttpClient"/>를 관리한다.
/// 네이버/Yahoo/KRX는 브라우저 User-Agent가 없으면 차단되므로 공통 헤더로 설정한다.
/// </summary>
public sealed class PriceSourceRegistry : IDisposable
{
    private readonly HttpClient _http;
    private readonly AppConfig _config;
    private readonly Dictionary<SourceKind, IPriceSource> _sources;
    private readonly NameResolver _nameResolver;
    private readonly KisPriceSource _kis;

    /// <summary>차트용 봉 데이터(분/일/주/월) 조회 서비스.</summary>
    public ChartDataService Chart { get; }

    public PriceSourceRegistry(AppConfig config, Action saveConfig)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

        _kis = new KisPriceSource(config, saveConfig, _http);
        _sources = new()
        {
            [SourceKind.Naver] = new NaverPriceSource(_http),
            [SourceKind.Yahoo] = new YahooPriceSource(_http),
            [SourceKind.Daum] = new DaumPriceSource(_http),
            [SourceKind.Kis] = _kis,
        };
        _nameResolver = new NameResolver(_http);
        Chart = new ChartDataService(_http, this);
    }

    /// <summary>KIS 일/주/월봉(차트용) — ChartDataService에서 위임 호출.</summary>
    public Task<List<Candle>> KisChartAsync(string code, DateTime from, DateTime to, char period, CancellationToken ct = default)
        => _kis.FetchChartAsync(code, from, to, period, ct);

    /// <summary>KIS 당일 1분봉(차트용) — ChartDataService에서 집계.</summary>
    public Task<List<Candle>> KisMinutesAsync(string code, CancellationToken ct = default)
        => _kis.FetchTodayMinutesAsync(code, ct);

    /// <summary>
    /// 모니터링용 현재가 — KIS 키가 있으면 KIS 실시간(inquire-price), 없으면 네이버 최근 종가로 폴백.
    /// 둘 다 실패하면 null.
    /// </summary>
    public async Task<Quote?> QuoteAsync(string code, CancellationToken ct = default)
    {
        if (_config.HasKisCredentials)
        {
            try { return await _kis.FetchQuoteAsync(code, ct); }
            catch { /* KIS 실패 시 네이버 폴백 */ }
        }
        var close = await CurrentCloseAsync(code, ct);
        return close.HasValue ? new Quote(code, close.Value, 0, DateTime.Now) : null;
    }

    /// <summary>현재가(가장 최근 종가) 조회 — 자산 평가용. 네이버(무인증·빠름) 우선, 실패 시 null.</summary>
    public async Task<decimal?> CurrentCloseAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var to = DateTime.Today;
            var series = await _sources[SourceKind.Naver].FetchAsync(code, to.AddDays(-12), to, ct);
            return series.Candles.Count > 0 ? series.Candles[^1].Close : null;
        }
        catch { return null; }
    }

    public IPriceSource Get(SourceKind kind) => _sources[kind];

    public IReadOnlyList<IPriceSource> All => _sources.Values.ToList();

    /// <summary>단축 종목코드 → 한글 종목명(못 찾으면 null).</summary>
    public Task<string?> LookupNameAsync(string code, CancellationToken ct = default)
        => _nameResolver.LookupAsync(code, ct);

    /// <summary>코드 또는 이름(일부)으로 종목 후보 검색.</summary>
    public Task<List<StockHit>> SearchAsync(string text, CancellationToken ct = default)
        => _nameResolver.SearchAsync(text, ct);

    public void Dispose() => _http.Dispose();
}
