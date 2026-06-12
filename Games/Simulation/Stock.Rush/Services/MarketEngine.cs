namespace StockRush.Services;

/// <summary>
/// 시장 시뮬레이션 엔진.
/// 1틱 = 장중 10초 (실시간 100ms) / 1일 = 09:00~15:30 (2,340틱 ≈ 실시간 4분)
/// 가격 = 랜덤워크 + 시장무드 + 섹터무드 + 뉴스드리프트, 상하한가 ±30%, VI 발동.
/// </summary>
public class MarketEngine
{
    public const int TicksPerDay = 2340;
    public const int TicksPerCandle = 30;   // 5분봉
    public const int ViHaltTicks = 120;     // VI 정지: 장중 20분 (실시간 12초)
    public const double ViThreshold = 0.08; // 30틱 내 ±8% 급변 시 VI

    private readonly Random _rng = new();
    private double _marketMood;
    private readonly Dictionary<Sector, double> _sectorMood = new();

    public List<Stock> Stocks { get; } = new();
    public int Day { get; private set; } = 1;
    public int TickCount { get; private set; }
    public bool SessionOpen { get; private set; }
    public TimeSpan MarketTime => TimeSpan.FromHours(9) + TimeSpan.FromSeconds(Math.Min(TickCount, TicksPerDay) * 10);

    /// <summary>코스피 흉내 지수 (시장 분위기 표시용)</summary>
    public double IndexValue { get; private set; } = 2500.0;
    public double IndexPrevClose { get; private set; } = 2500.0;

    public event Action<Stock>? ViTriggered;
    public event Action? DayClosed;

    public MarketEngine()
    {
        foreach (Sector s in Enum.GetValues<Sector>()) _sectorMood[s] = 0;
        CreateStocks();
        foreach (var s in Stocks) BeginDay(s, s.PrevClose);
    }

    private void CreateStocks()
    {
        void Add(string code, string name, Sector sector, long price, double vol)
        {
            var s = new Stock { Code = code, Name = name, Sector = sector, Volatility = vol };
            s.PrevClose = price;
            s.RawPrice = price;
            s.Price = price;
            Stocks.Add(s);
        }

        Add("005430", "한빛전자",     Sector.반도체,   72_400, 0.0011);
        Add("091200", "대한반도체",   Sector.반도체,  156_000, 0.0014);
        Add("033870", "서울디스플레이", Sector.반도체,   18_350, 0.0016);
        Add("214300", "코어바이오",   Sector.바이오,   84_200, 0.0024);
        Add("196800", "메디퓨처",     Sector.바이오,   42_750, 0.0028);
        Add("373100", "에코배터리",   Sector.이차전지, 412_500, 0.0018);
        Add("450140", "리튬테크",     Sector.이차전지,  98_700, 0.0022);
        Add("263700", "픽셀게임즈",   Sector.게임,     54_300, 0.0019);
        Add("377300", "넥스트AI",     Sector.게임,    128_500, 0.0023);
        Add("105560", "한국홀딩스",   Sector.금융,     61_800, 0.0007);
        Add("086790", "하나로금융",   Sector.금융,     43_150, 0.0008);
        Add("011170", "대성화학",     Sector.화학,    187_500, 0.0012);
        Add("096770", "그린에너지",   Sector.화학,     76_300, 0.0015);
        Add("009540", "해양중공업",   Sector.중공업,  134_200, 0.0014);
        Add("042660", "한울조선",     Sector.중공업,   38_450, 0.0017);
        Add("352820", "스타엔터",     Sector.엔터,    215_000, 0.0019);
        Add("035900", "뮤직웨이브",   Sector.엔터,     67_400, 0.0021);
        Add("282330", "마켓플러스",   Sector.유통,    142_000, 0.0010);
        Add("017670", "텔레콤원",     Sector.통신,     52_900, 0.0006);
        Add("030200", "코넥트통신",   Sector.통신,     31_250, 0.0007);
    }

    // ── KRX 호가단위 ──────────────────────────────────────────────
    public static long TickSize(long price) => price switch
    {
        < 2_000 => 1,
        < 5_000 => 5,
        < 20_000 => 10,
        < 50_000 => 50,
        < 200_000 => 100,
        < 500_000 => 500,
        _ => 1_000
    };

    public static long RoundToTick(double price, bool roundUp)
    {
        var p = (long)Math.Max(1, Math.Round(price));
        var t = TickSize(p);
        return roundUp ? (p + t - 1) / t * t : p / t * t;
    }

    public static long UpperLimit(long prevClose) => RoundToTick(prevClose * 1.3, false);
    public static long LowerLimit(long prevClose) => Math.Max(TickSize(prevClose), RoundToTick(prevClose * 0.7, true));

    // ── 세션 제어 ─────────────────────────────────────────────────
    public void OpenSession()
    {
        TickCount = 0;
        SessionOpen = true;
    }

    /// <summary>오버나이트 갭 반영 후 다음 날 시작</summary>
    public void NextDay()
    {
        Day++;
        IndexPrevClose = IndexValue;
        foreach (var s in Stocks)
        {
            s.NewsDrift = 0;
            s.NewsDriftTicks = 0;
            s.HaltTicks = 0;
            s.RecentPrices.Clear();
            s.PrevClose = s.Price;
            var gap = Gauss() * 0.012 + _marketMood * 30;
            gap = Math.Clamp(gap, -0.08, 0.08);
            var open = Math.Clamp(s.RawPrice * (1 + gap), s.PrevClose * 0.7, s.PrevClose * 1.3);
            BeginDay(s, RoundToTick(open, false));
        }
        OpenSession();
    }

    private void BeginDay(Stock s, long openPrice)
    {
        s.Candles.Clear();
        s.CurrentCandle = null;
        s.RawPrice = openPrice;
        s.Price = openPrice;
        s.DayOpen = openPrice;
        s.DayHigh = openPrice;
        s.DayLow = openPrice;
        s.AccVolume = 0;
        s.StateText = "";
        s.RefreshDerived();
    }

    // ── 틱 진행 ──────────────────────────────────────────────────
    public void Tick()
    {
        if (!SessionOpen) return;
        TickCount++;

        _marketMood = _marketMood * 0.99 + Gauss() * 0.00002;
        foreach (var key in _sectorMood.Keys.ToList())
            _sectorMood[key] = _sectorMood[key] * 0.985 + Gauss() * 0.00005;

        double indexRet = 0;
        foreach (var s in Stocks)
        {
            UpdateStock(s);
            indexRet += s.ChangeRate;
        }
        IndexValue = IndexPrevClose * (1 + indexRet / Stocks.Count / 100.0);

        if (TickCount >= TicksPerDay)
        {
            SessionOpen = false;
            DayClosed?.Invoke();
        }
    }

    private void UpdateStock(Stock s)
    {
        if (s.HaltTicks > 0)
        {
            s.HaltTicks--;
            if (s.HaltTicks == 0) s.StateText = s.IsUpperLimit ? "상한" : s.IsLowerLimit ? "하한" : "";
            return;
        }

        var drift = 0.0;
        if (s.NewsDriftTicks > 0)
        {
            drift = s.NewsDrift;
            s.NewsDriftTicks--;
            if (s.NewsDriftTicks == 0) s.NewsDrift = 0;
        }

        var ret = Gauss() * s.Volatility + drift + _marketMood + _sectorMood[s.Sector];
        var upper = UpperLimit(s.PrevClose);
        var lower = LowerLimit(s.PrevClose);

        s.RawPrice = Math.Clamp(s.RawPrice * (1 + ret), lower, upper);
        var newPrice = Math.Clamp(RoundToTick(s.RawPrice, false), lower, upper);

        var dir = newPrice.CompareTo(s.Price);
        s.Price = newPrice;
        if (newPrice > s.DayHigh) s.DayHigh = newPrice;
        if (newPrice < s.DayLow) s.DayLow = newPrice;
        s.TrendBrush = Ui.ForChange(s.Change);
        s.RefreshDerived();

        // 거래량: 변동 클수록 폭증
        var volBase = 200 + (long)(Math.Abs(ret) * 2_000_000) + _rng.Next(0, 800);
        if (Math.Abs(drift) > 0.0005) volBase *= 4;
        s.AccVolume += volBase;

        // 캔들 갱신
        if (s.CurrentCandle == null || TickCount % TicksPerCandle == 1)
        {
            if (s.CurrentCandle != null) s.Candles.Add(s.CurrentCandle);
            s.CurrentCandle = new Candle
            {
                Time = MarketTime,
                Open = newPrice, High = newPrice, Low = newPrice, Close = newPrice, Volume = volBase
            };
        }
        else
        {
            var c = s.CurrentCandle;
            c.Close = newPrice;
            if (newPrice > c.High) c.High = newPrice;
            if (newPrice < c.Low) c.Low = newPrice;
            c.Volume += volBase;
        }

        // 상태 뱃지
        if (s.IsUpperLimit) s.StateText = "상한";
        else if (s.IsLowerLimit) s.StateText = "하한";
        else if (s.StateText is "상한" or "하한") s.StateText = "";

        // VI 판정: 최근 30틱 대비 ±8% 급변
        s.RecentPrices.Enqueue(newPrice);
        while (s.RecentPrices.Count > 30) s.RecentPrices.Dequeue();
        if (s.RecentPrices.Count >= 30 && !s.IsUpperLimit && !s.IsLowerLimit)
        {
            var oldP = s.RecentPrices.Peek();
            if (oldP > 0 && Math.Abs((double)(newPrice - oldP) / oldP) >= ViThreshold)
            {
                s.HaltTicks = ViHaltTicks;
                s.StateText = "VI";
                s.RecentPrices.Clear();
                ViTriggered?.Invoke(s);
            }
        }

        _ = dir;
    }

    // ── 뉴스 충격 ─────────────────────────────────────────────────
    /// <summary>총 impact만큼 durationTicks에 걸쳐 가격에 반영. 속보는 즉시 갭 포함.</summary>
    public void ApplyShock(Stock s, double totalImpact, int durationTicks, double instantGap = 0)
    {
        if (instantGap != 0 && s.HaltTicks == 0)
        {
            var upper = UpperLimit(s.PrevClose);
            var lower = LowerLimit(s.PrevClose);
            s.RawPrice = Math.Clamp(s.RawPrice * (1 + instantGap), lower, upper);
            s.Price = Math.Clamp(RoundToTick(s.RawPrice, false), lower, upper);
            if (s.Price > s.DayHigh) s.DayHigh = s.Price;
            if (s.Price < s.DayLow) s.DayLow = s.Price;
            s.RefreshDerived();
        }
        s.NewsDrift = totalImpact / Math.Max(1, durationTicks);
        s.NewsDriftTicks = durationTicks;
    }

    public void ApplySectorShock(Sector sector, double totalImpact, int durationTicks)
    {
        foreach (var s in Stocks.Where(x => x.Sector == sector))
            ApplyShock(s, totalImpact * (0.7 + _rng.NextDouble() * 0.6), durationTicks);
    }

    public void ApplyMarketShock(double totalImpact, int durationTicks)
    {
        foreach (var s in Stocks)
            ApplyShock(s, totalImpact * (0.5 + _rng.NextDouble() * 1.0), durationTicks);
    }

    // ── 호가창 생성 ───────────────────────────────────────────────
    /// <summary>(asks 10단계 내림차순, bids 10단계) 생성 — 현재가 기준</summary>
    public void FillOrderBook(Stock s, List<BookRow> rows)
    {
        // rows: [0..9] 매도호가(위, 높은가격→낮은가격), [10..19] 매수호가
        var maxQty = 1L;
        var basePrice = s.Price;
        for (var i = 0; i < 10; i++)
        {
            var ask = rows[9 - i];
            var p = basePrice;
            for (var k = 0; k <= i; k++) p += TickSize(p);
            ask.Price = p;
            ask.Qty = NextBookQty(s, i);
            ask.PrevClose = s.PrevClose;
            if (ask.Qty > maxQty) maxQty = ask.Qty;

            var bid = rows[10 + i];
            var bp = basePrice;
            for (var k = 0; k < i; k++) bp -= TickSize(bp);
            bid.Price = Math.Max(TickSize(basePrice), bp);
            bid.Qty = NextBookQty(s, i);
            bid.PrevClose = s.PrevClose;
            if (bid.Qty > maxQty) maxQty = bid.Qty;
        }
        foreach (var r in rows)
        {
            r.MaxQty = maxQty;
            r.Refresh();
        }
    }

    private long NextBookQty(Stock s, int depth)
    {
        var scale = Math.Max(50, 60_000_000.0 / Math.Max(1, s.Price));
        var q = (long)(scale * (0.3 + _rng.NextDouble() * 1.4) * (1 + depth * 0.15));
        return Math.Max(1, q);
    }

    /// <summary>시장가 체결가 계산: 잔량 잠식 슬리피지 포함</summary>
    public long FillPrice(Stock s, OrderSide side, long qty)
    {
        var p = s.Price;
        var t = TickSize(p);
        var levelQty = Math.Max(50, 60_000_000 / Math.Max(1, s.Price));
        var slipTicks = (int)Math.Min(3, qty / Math.Max(1, levelQty));
        return side == OrderSide.매수
            ? Math.Min(UpperLimit(s.PrevClose), p + t * (1 + slipTicks))
            : Math.Max(LowerLimit(s.PrevClose), p - t * (1 + slipTicks));
    }

    public Stock? Find(string code) => Stocks.FirstOrDefault(s => s.Code == code);

    public double Gauss()
    {
        var u1 = 1.0 - _rng.NextDouble();
        var u2 = _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    public Random Rng => _rng;
}
