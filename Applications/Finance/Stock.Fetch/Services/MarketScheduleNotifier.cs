namespace Stock.Fetch.Services;

/// <summary>장 세션 구분(알림 지역).</summary>
public enum MarketRegion { KR, US }

/// <summary>
/// 한국·미국 장 세션(오픈·동시호가·마감 등) <b>5분 전</b>에 Slack·트레이로 알림한다.
/// <list type="bullet">
/// <item>한국(KST 고정): 프리장 08:00, 장전 동시호가 08:50, 정규장 09:00, 장마감 동시호가 15:20, 애프터마켓 마감 20:00.</item>
/// <item>미국(ET 앵커 → 서머타임 자동): 데이마켓 ET20:00(≈KST09:00), 프리마켓 ET04:00(≈KST17:00),
///       정규장 ET09:00(≈KST22:00), 애프터마켓 ET16:00(≈KST05:00). 표준시엔 KST가 자동으로 +1시간.</item>
/// </list>
/// 평일만(주말 제외 · 공휴일 미반영). 30초마다 점검하며 이벤트/날짜별 1회만 발송. 앱을 늦게 켜서 이미 10분 넘게
/// 지난 알림은 건너뛴다. 이벤트는 백그라운드 스레드에서 발생하므로 UI 구독자는 Dispatcher 마샬링이 필요하다.
/// </summary>
public sealed class MarketScheduleNotifier(AppConfig config, SlackNotifier slack) : IDisposable
{
    public event Action<string, string>? Raised;   // title, detail (트레이)
    public event Action<string>? StatusChanged;

    private const int LeadMinutes = 5;

    // (지역, 라벨, 시, 분) — KR은 KST, US는 ET 벽시계 기준.
    private static readonly (MarketRegion Region, string Label, int H, int M)[] Events =
    {
        (MarketRegion.KR, "프리장 오픈",        8, 0),
        (MarketRegion.KR, "장전 동시호가",      8, 50),
        (MarketRegion.KR, "정규장 시작",        9, 0),
        (MarketRegion.KR, "장마감 동시호가",    15, 20),
        (MarketRegion.KR, "시간외 단일가 마감", 18, 0),
        (MarketRegion.KR, "애프터마켓 마감",    20, 0),
        (MarketRegion.US, "데이마켓 시작",      20, 0),   // ET 20:00 ≈ KST 09:00(서머타임)
        (MarketRegion.US, "프리마켓 시작",      4, 0),    // ET 04:00 ≈ KST 17:00
        (MarketRegion.US, "정규장 시작",        9, 30),   // ET 09:30 ≈ KST 22:30
        (MarketRegion.US, "애프터마켓 시작",    16, 0),   // ET 16:00 ≈ KST 05:00
    };

    private CancellationTokenSource? _cts;
    private readonly HashSet<string> _fired = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { Tick(); }
            catch (Exception ex) { StatusChanged?.Invoke("오류: " + ex.Message); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Tick()
    {
        if (!config.MarketScheduleAlerts) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today != _day) { _fired.Clear(); _day = today; }

        var nowLocal = DateTime.Now;
        var nowUtc = DateTime.UtcNow;

        foreach (var e in Events)
        {
            DateTime eventLocal;
            DayOfWeek dow;
            string dateKey;

            if (e.Region == MarketRegion.KR)
            {
                eventLocal = nowLocal.Date + new TimeSpan(e.H, e.M, 0);   // 로컬=KST
                dow = eventLocal.DayOfWeek;
                dateKey = eventLocal.ToString("yyyyMMdd");
            }
            else
            {
                // ET 벽시계로 오늘 이벤트 → 절대시각(UTC) → 로컬(KST)로 환산. 서머타임 자동.
                var etNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, UsMarket.Eastern);
                var etEvt = new DateTime(etNow.Year, etNow.Month, etNow.Day, e.H, e.M, 0, DateTimeKind.Unspecified);
                dow = etEvt.DayOfWeek;
                var utc = TimeZoneInfo.ConvertTimeToUtc(etEvt, UsMarket.Eastern);
                eventLocal = utc.ToLocalTime();
                dateKey = etEvt.ToString("yyyyMMdd");
            }

            if (dow is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;   // 주말 제외

            var alertLocal = eventLocal.AddMinutes(-LeadMinutes);
            if (nowLocal < alertLocal) continue;
            if ((nowLocal - alertLocal).TotalMinutes > 10) continue;       // 지난 알림(늦게 켬 등) 무시

            if (!_fired.Add($"{e.Region}|{e.Label}|{dateKey}")) continue;  // 이벤트/날짜별 1회

            string region = e.Region == MarketRegion.KR ? "한국 증시" : "미국 증시";
            string title = $"{region} {e.Label} 5분 전";
            string detail = e.Region == MarketRegion.KR
                ? $"{eventLocal:HH:mm} KST 예정"
                : $"{eventLocal:HH:mm} KST 예정 (ET {e.H:00}:{e.M:00})";

            Raised?.Invoke(title, detail);
            _ = SafeAsync(() => slack.SendMarketScheduleAsync(title, detail));
        }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { StatusChanged?.Invoke("Slack 전송 오류: " + ex.Message); }
    }

    public void Dispose() => _cts?.Cancel();
}
