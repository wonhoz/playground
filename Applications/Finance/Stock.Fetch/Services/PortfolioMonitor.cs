using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 보유 종목을 주기적으로 KIS 현재가로 조회해, 평단 대비 수익률이 설정 임계값(±2/5/7/10/12% 등)을
/// <b>새로 넘어설 때(엣지)</b>만 알림을 발생시킨다. 같은 임계값은 다시 아래로 내려갔다 재돌파해야 재알림(도배 방지).
/// 장 시간(평일 09:00~15:30) 게이팅·일일 상태 리셋. 이벤트는 백그라운드 스레드에서 발생하므로 UI는 마샬링 필요.
/// </summary>
public sealed class PortfolioMonitor(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack) : IDisposable
{
    private CancellationTokenSource? _cts;
    // 종목별 '직전 폴링에서 도달해 있던' 임계값 집합(상향/하향 분리). 엣지 검출용.
    private readonly Dictionary<string, HashSet<double>> _prevUp = new();
    private readonly Dictionary<string, HashSet<double>> _prevDown = new();
    // 첫 폴링을 마친 종목(시작 시엔 가장 큰 임계값만 1회 알림하고 나머지는 시드).
    private readonly HashSet<string> _seeded = new();
    private DateOnly _stateDate = DateOnly.FromDateTime(DateTime.Today);

    public event Action<PortfolioAlert>? AlertRaised;
    public event Action<string>? StatusChanged;

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
        _prevUp.Clear();
        _prevDown.Clear();
        _seeded.Clear();   // 재시작 시 다시 가장 큰 임계값만 1회 알림하도록
        StatusChanged?.Invoke("모니터링 중지됨");
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        StatusChanged?.Invoke("모니터링 시작");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!config.MonitorMarketHoursOnly || IsMarketOpen(DateTime.Now))
                    await PollAsync(ct);
                else
                    StatusChanged?.Invoke($"장 시간 외 대기 중 ({DateTime.Now:HH:mm})");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { StatusChanged?.Invoke("오류: " + ex.Message); }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, config.MonitorIntervalSeconds)), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        ResetIfNewDay();
        var pf = PortfolioStore.Load(config);
        var holdings = PortfolioStore.Holdings(pf).Where(h => h.Quantity > 0 && h.AvgPrice > 0).ToList();
        if (holdings.Count == 0) { StatusChanged?.Invoke("보유 종목 없음"); return; }

        var thresholds = config.AlertThresholds.Where(t => t > 0).OrderBy(t => t).ToList();
        foreach (var h in holdings)
        {
            ct.ThrowIfCancellationRequested();
            Quote? q;
            try { q = await registry.QuoteAsync(h.Code, ct); }
            catch { continue; }
            if (q is null || q.Price <= 0) continue;

            double ret = (double)(q.Price / h.AvgPrice - 1) * 100;
            Evaluate(h, q.Price, ret, thresholds);

            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }
        StatusChanged?.Invoke($"갱신 {DateTime.Now:HH:mm:ss} · 보유 {holdings.Count}종목");
    }

    private void Evaluate(Holding h, decimal price, double ret, List<double> thresholds)
    {
        var nowUp = thresholds.Where(t => ret >= t).ToHashSet();
        var nowDown = thresholds.Where(t => ret <= -t).ToHashSet();

        // 첫 폴링: 이미 여러 임계값을 넘어 있어도 가장 큰 것 1회만 알림(도배 방지) 후 나머지는 시드.
        if (!_seeded.Contains(h.Code))
        {
            _seeded.Add(h.Code);
            if (nowUp.Count > 0) Raise(h, price, ret, +nowUp.Max());
            else if (nowDown.Count > 0) Raise(h, price, ret, -nowDown.Max());
            _prevUp[h.Code] = nowUp;
            _prevDown[h.Code] = nowDown;
            return;
        }

        var prevUp = _prevUp.GetValueOrDefault(h.Code) ?? new HashSet<double>();
        var prevDown = _prevDown.GetValueOrDefault(h.Code) ?? new HashSet<double>();

        foreach (var t in nowUp.Except(prevUp).OrderBy(x => x))
            Raise(h, price, ret, +t);
        foreach (var t in nowDown.Except(prevDown).OrderBy(x => x))
            Raise(h, price, ret, -t);

        _prevUp[h.Code] = nowUp;
        _prevDown[h.Code] = nowDown;
    }

    private void Raise(Holding h, decimal price, double ret, double signedThreshold)
    {
        var a = new PortfolioAlert(h.Code, h.Name, price, h.AvgPrice, h.Quantity, ret, signedThreshold, DateTime.Now);
        AlertRaised?.Invoke(a);
        _ = SendSlackSafeAsync(a);
    }

    private async Task SendSlackSafeAsync(PortfolioAlert a)
    {
        try { await slack.SendAsync(a); }
        catch (Exception ex) { StatusChanged?.Invoke("Slack 전송 오류: " + ex.Message); }
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today == _stateDate) return;
        _prevUp.Clear();
        _prevDown.Clear();
        _seeded.Clear();   // 새 날 첫 폴링도 가장 큰 임계값만 1회 알림
        _stateDate = today;
    }

    /// <summary>한국 정규장 09:00~15:30, 평일.</summary>
    public static bool IsMarketOpen(DateTime now)
    {
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var t = now.TimeOfDay;
        return t >= new TimeSpan(9, 0, 0) && t <= new TimeSpan(15, 30, 0);
    }

    public void Dispose() => _cts?.Cancel();
}
