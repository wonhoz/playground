using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 국내 종목의 매수/익절 래더·갭다운 알림 엔진. 보유 모니터·관심 모니터가 <b>공유</b>하는 단일 인스턴스로,
/// 같은 코드가 양쪽에 있어도 알림은 1회만 발생한다.
/// <list type="bullet">
/// <item>래더 레벨(매수 4호가·익절 4방식·갭다운선)은 완성된 일봉 기준이라 <b>하루 1회</b>만 계산·캐시하고,
///       장중엔 현재가와 비교만 한다("오늘 제외·최소 11일·추세 자동반영" 방법론).</item>
/// <item>매수 호가 닿음: 현재가 ≤ 매수지정가(호가별 1일 1회). 보유·관심 공통.</item>
/// <item>익절 돌파: 보유 종목 + 추세점 상승(&gt;0.33)일 때, 현재가 ≥ 익절가(방식별 1일 1회).</item>
/// <item>갭다운: 오늘 시초가 ≤ 갭다운 취소선(전일 정규종가 −5%)이면 1회.</item>
/// </list>
/// 알림은 <see cref="Raised"/> 이벤트(트레이)와 Slack으로 동시 전송.
/// </summary>
public sealed class LadderAlertEngine(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack)
{
    public event Action<LadderAlert>? Raised;

    private sealed class Levels
    {
        public decimal[] Buys = Array.Empty<decimal>();
        public SellTarget[] Targets = Array.Empty<SellTarget>();
        public decimal Gap;
        public bool TrendUp;
        public bool HasHolding;
        public decimal TodayOpen;      // 오늘 시초가(0=아직 미형성)
        public bool OpenChecked;       // 갭다운 판정 완료
        public DateTime LastOpenTry = DateTime.MinValue;
        public bool Failed;            // 데이터 부족 등 당일 스킵
    }

    private readonly Dictionary<string, Levels> _cache = new();
    private readonly HashSet<string> _fired = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>국내 종목·옵트인일 때만 호출한다(호출 측에서 게이팅).</summary>
    public async Task EvaluateAsync(string code, string name, decimal price, CancellationToken ct = default)
    {
        if (price <= 0) return;
        ResetIfNewDay();

        var lv = await GetOrBuildAsync(code, ct);
        if (lv.Failed) return;

        // 1) 매수 호가 닿음(보유·관심 공통)
        for (int k = 0; k < lv.Buys.Length; k++)
            if (lv.Buys[k] > 0 && price <= lv.Buys[k])
                Fire(code, name, LadderAlertKind.BuyTouch, k + 1, price, lv.Buys[k], $"매수 {k + 1}호가 {lv.Buys[k]:N0}원 도달");

        // 2) 익절 돌파(보유 + 상승세)
        if (lv.HasHolding && lv.TrendUp)
            for (int k = 0; k < lv.Targets.Length; k++)
            {
                var t = lv.Targets[k];
                if (t.Price > 0 && price >= t.Price)
                    Fire(code, name, LadderAlertKind.SellBreak, k + 1, price, t.Price, $"익절 '{t.Name}' {t.Price:N0}원 돌파");
            }

        // 3) 갭다운(시초가)
        if (!lv.OpenChecked)
            await CheckGapAsync(code, name, lv, ct);
    }

    // ───────────────────────── 래더 캐시 ─────────────────────────
    private async Task<Levels> GetOrBuildAsync(string code, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(code, out var cached)) return cached;

            var lv = new Levels();
            try
            {
                var daily = await registry.KrDailyAsync(code, 40, ct);
                var completed = daily.Where(c => c.Date.Date != DateTime.Today).ToList();
                if (completed.Count < LadderCalculator.RequiredDays) { lv.Failed = true; }
                else
                {
                    // 오늘 제외 + 최소 11일 + 추세 자동반영.
                    var win = completed.TakeLast(LadderCalculator.RequiredDays).ToList();
                    var series = new StockSeries(code, "", "", SourceKind.Naver, win);
                    var holding = PortfolioStore.HoldingOf(PortfolioStore.Load(config), code);
                    var r = LadderCalculator.Calculate(series,
                        new LadderParams(0, 0, UseTrend: true, holding?.Quantity ?? 0, holding?.AvgPrice ?? 0m));
                    lv.Buys = r.BuyPrices;
                    lv.Targets = r.SellTargets;
                    lv.Gap = r.GapCancelLine;
                    lv.TrendUp = r.TrendScore > 0.33;
                    lv.HasHolding = holding is { Quantity: > 0, AvgPrice: > 0 };
                    var today = daily.FirstOrDefault(c => c.Date.Date == DateTime.Today);
                    lv.TodayOpen = today?.Open ?? 0m;
                }
            }
            catch { lv.Failed = true; }

            _cache[code] = lv;
            return lv;
        }
        finally { _gate.Release(); }
    }

    // ───────────────────────── 갭다운(시초가) ─────────────────────────
    private async Task CheckGapAsync(string code, string name, Levels lv, CancellationToken ct)
    {
        // 시초가 미형성 시, 평일 장 시작 이후에만 재조회(과도한 호출 방지 위해 30초 스로틀).
        if (lv.TodayOpen <= 0)
        {
            var now = DateTime.Now;
            bool weekday = now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            if (!weekday || now.TimeOfDay < new TimeSpan(9, 0, 0)) return;
            if ((now - lv.LastOpenTry).TotalSeconds < 30) return;
            lv.LastOpenTry = now;
            try
            {
                var daily = await registry.KrDailyAsync(code, 5, ct);
                var today = daily.FirstOrDefault(c => c.Date.Date == DateTime.Today);
                lv.TodayOpen = today?.Open ?? 0m;
            }
            catch { return; }
            if (lv.TodayOpen <= 0) return;
        }

        lv.OpenChecked = true;
        if (lv.Gap > 0 && lv.TodayOpen <= lv.Gap)
            Fire(code, name, LadderAlertKind.GapDown, 0, lv.TodayOpen, lv.Gap,
                $"시초가 {lv.TodayOpen:N0}원 ≤ 갭다운 취소선 {lv.Gap:N0}원 — 당일 매수 취소 검토");
    }

    // ───────────────────────── 발생/전송 ─────────────────────────
    private void Fire(string code, string name, LadderAlertKind kind, int level, decimal price, decimal target, string detail)
    {
        if (!_fired.Add($"{code}|{kind}|{level}")) return;   // 1일 1회(호가/방식별)
        var a = new LadderAlert(code, name, kind, level, price, target, detail, DateTime.Now);
        Raised?.Invoke(a);
        _ = SafeSlackAsync(a);
    }

    private async Task SafeSlackAsync(LadderAlert a)
    {
        try { await slack.SendLadderAlertAsync(a); }
        catch { /* Slack 실패는 무시 */ }
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today == _day) return;
        _gate.Wait();
        try { _cache.Clear(); _fired.Clear(); _day = today; }
        finally { _gate.Release(); }
    }
}
