using System.Net.Http;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

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
    private readonly YahooPriceSource _yahoo;
    private readonly FinnhubPriceSource _finnhub;
    private readonly AlpacaPriceSource _alpaca;
    private readonly DatabentoPriceSource _databento;

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
        _yahoo = new YahooPriceSource(_http);
        _finnhub = new FinnhubPriceSource(config, _http);
        _alpaca = new AlpacaPriceSource(config, _http);
        _databento = new DatabentoPriceSource(config, _http);
        _sources = new()
        {
            [SourceKind.Naver] = new NaverPriceSource(_http),
            [SourceKind.Yahoo] = _yahoo,
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

    /// <summary>KIS 최근 1분봉(모니터링용 경량 조회) — 반등 시그널 엔진에서 증분 병합.</summary>
    public Task<List<Candle>> KisRecentMinutesAsync(string code, int minBars, CancellationToken ct = default)
        => _kis.FetchRecentMinutesAsync(code, minBars, ct);

    /// <summary>KIS 특정 일자 1분봉 전체(과거 일자 지원) — 분봉 CSV 내보내기용.</summary>
    public Task<List<Candle>> KisDayMinutesAsync(string code, DateTime date, CancellationToken ct = default)
        => _kis.FetchDayMinutesAsync(code, date, ct);

    /// <summary>KIS 실시간 호가창(10단) — 즐겨찾기 실시간 수급·호가 창용.</summary>
    public Task<MarketDepth> KrMarketDepthAsync(string code, CancellationToken ct = default)
        => _kis.FetchMarketDepthAsync(code, ct);

    /// <summary>KIS 실시간 수급(외인/기관/개인/프로그램 순매수·소진율·체결강도) — 즐겨찾기 실시간 수급·호가 창용.</summary>
    public Task<SupplyDemand> KrSupplyDemandAsync(string code, CancellationToken ct = default)
        => _kis.FetchSupplyDemandAsync(code, ct);

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

    /// <summary>
    /// 관심 종목(워치리스트) 현재가·등락율 조회. 종목의 시장(KR/US)·소스(Naver/Yahoo/KIS)에 따라 분기한다.
    /// </summary>
    public async Task<Quote> WatchQuoteAsync(WatchItem item, CancellationToken ct = default)
    {
        // 국내 업종/지수(코스피 0001 등)는 KIS 지수 엔드포인트로 조회.
        if (item.IsIndex)
            return await _kis.FetchIndexQuoteAsync(item.Symbol, ct);

        if (item.Market == MarketKind.US)
        {
            // 프리/애프터마켓엔 Yahoo로 고정(설정 시) — Finnhub·Alpaca 무료는 확장시간을 잘 주지 않으므로.
            var src = _config.WatchUseYahooExtended && UsMarket.IsExtended() ? WatchSource.Yahoo : item.Source;
            return src switch
            {
                WatchSource.Kis => await _kis.FetchOverseasQuoteAsync(item.Exchange, item.Symbol, ct),
                WatchSource.Finnhub => await _finnhub.FetchQuoteAsync(item.Symbol, ct),
                WatchSource.Alpaca => await _alpaca.FetchQuoteAsync(item.Symbol, ct),
                WatchSource.Databento => await _databento.FetchQuoteAsync(item.Symbol, ct),
                _ => await _yahoo.FetchQuoteAsync(item.Symbol, ct), // Yahoo 기본
            };
        }

        // 국내
        if (item.Source == WatchSource.Kis)
            return await _kis.FetchQuoteAsync(item.Symbol, ct);

        // 네이버: 최근 2봉 종가로 전일 대비 등락율 계산.
        var to = DateTime.Today;
        var series = await _sources[SourceKind.Naver].FetchAsync(item.Symbol, to.AddDays(-12), to, ct);
        var c = series.Candles;
        if (c.Count == 0) throw new PriceSourceException($"네이버에서 종목 '{item.Symbol}' 시세를 찾지 못했습니다.");
        decimal price = c[^1].Close;
        decimal prev = c.Count >= 2 ? c[^2].Close : price;
        decimal rate = prev > 0 ? Math.Round((price / prev - 1) * 100, 2) : 0m;
        return new Quote(item.Symbol, price, rate, DateTime.Now);
    }

    /// <summary>
    /// 프록시(선물·지수·개별주) 현재가·등락율을 소스별로 직접 조회 — 야간 프록시 선행 알림용.
    /// Yahoo는 NQ=F·^KS11 같은 특수 티커도 처리한다(IsIndex 분기 없이 소스 그대로). 실패 시 null.
    /// </summary>
    public async Task<Quote?> ProxyQuoteAsync(string symbol, WatchSource source, CancellationToken ct = default)
    {
        try
        {
            return source switch
            {
                WatchSource.Yahoo => await _yahoo.FetchQuoteAsync(symbol, ct),
                WatchSource.Kis => await _kis.FetchQuoteAsync(symbol, ct),
                WatchSource.Finnhub => await _finnhub.FetchQuoteAsync(symbol, ct),
                WatchSource.Alpaca => await _alpaca.FetchQuoteAsync(symbol, ct),
                WatchSource.Databento => await _databento.FetchQuoteAsync(symbol, ct),
                _ => null,
            };
        }
        catch { return null; }
    }

    /// <summary>국내 일봉(지정 구간) — 백테스트의 과거 시점 일봉 추세 재현용. 네이버(무인증) 사용.</summary>
    public async Task<IReadOnlyList<Candle>> KrDailyRangeAsync(string code, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var series = await _sources[SourceKind.Naver].FetchAsync(code, from, to, ct);
        return series.Candles;
    }

    /// <summary>국내 일봉(최근 days일) — 래더/갭다운/반등 계산용. 네이버(무인증) 사용.</summary>
    public async Task<IReadOnlyList<Candle>> KrDailyAsync(string code, int days = 40, CancellationToken ct = default)
    {
        var to = DateTime.Today;
        var series = await _sources[SourceKind.Naver].FetchAsync(code, to.AddDays(-days), to, ct);
        return series.Candles;
    }

    /// <summary>미국 일봉(무접미사 티커) — 반등 지표 계산용. Yahoo(무인증) 사용.</summary>
    public Task<List<Candle>> UsDailyAsync(string symbol, string range = "6mo", CancellationToken ct = default)
        => _yahoo.FetchDailyCandlesAsync(symbol, range, ct);

    // ───────────────────────── 급등락 전광판(시장 전체 순위) ─────────────────────────

    /// <summary>
    /// 국내 전광판 순위 — KIS 실시간 순위 API(급등/급락=등락률 순위 · 거래량 급증=거래증가율 순위). KIS 키 필요.
    /// </summary>
    public Task<List<MoverRow>> KrMoversAsync(MoverKind kind, CancellationToken ct = default)
        => kind == MoverKind.VolumeSurge ? _kis.FetchVolumeRankAsync(ct)
         : _kis.FetchFluctuationRankAsync(gainers: kind == MoverKind.Gainers, ct);

    /// <summary>
    /// 미국 전광판 순위 — Alpaca 키가 있으면 실시간 스크리너(movers/most-actives · Yahoo는 정규장 지연),
    /// 없으면 Yahoo 사전정의 스크리너 폴백(지연 가능).
    /// </summary>
    public Task<List<MoverRow>> UsMoversAsync(MoverKind kind, CancellationToken ct = default)
    {
        if (_config.HasAlpacaKeys)
            return kind == MoverKind.VolumeSurge ? _alpaca.FetchMostActivesAsync(30, ct)
                 : _alpaca.FetchMoversAsync(gainers: kind == MoverKind.Gainers, 30, ct);
        string scrId = kind switch
        {
            MoverKind.Gainers => "day_gainers",
            MoverKind.Losers => "day_losers",
            _ => "most_actives",
        };
        return _yahoo.FetchPredefinedMoversAsync(scrId, 30, ct);
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
