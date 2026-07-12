using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly PriceSourceRegistry _registry;
    private StockSeries? _series;

    // 트레이 상주 + 보유 종목 모니터링
    private readonly SlackNotifier _slack;
    private readonly LadderAlertEngine _ladder;
    private readonly MinuteSignalEngine _minuteSignal;
    private readonly ReversalEstimator _reversal;
    private readonly PortfolioMonitor _monitor;
    private readonly WatchlistMonitor _watch;
    private readonly MarketScheduleNotifier _schedule;
    private readonly TrayManager _tray;
    private bool _reallyExit;
    private bool _trayHintShown;

    /// <summary>출력 포맷 콤보용 항목.</summary>
    private sealed record FormatItem(string Label, ExportFormat Format)
    {
        public override string ToString() => Label;
    }

    public MainWindow()
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        _config = AppConfig.Load();
        _registry = new PriceSourceRegistry(_config, () => _config.Save());

        // 데이터 소스 콤보 채우기
        // 커스텀 ComboBox 템플릿과 DisplayMemberPath 충돌 → ToString() 오버라이드로 라벨 표시
        SourceCombo.ItemsSource = _registry.All;
        SourceCombo.SelectedItem = _registry.Get(_config.LastSource);
        SourceCombo.SelectionChanged += SourceCombo_SelectionChanged;

        // 출력 포맷 콤보
        FormatCombo.ItemsSource = new[]
        {
            new FormatItem("CSV (쉼표 구분)", ExportFormat.Csv),
            new FormatItem("TSV (탭 구분)", ExportFormat.Tsv),
            new FormatItem("JSON", ExportFormat.Json),
            new FormatItem("XML", ExportFormat.Xml),
            new FormatItem("Markdown 표", ExportFormat.Markdown),
        };

        // 즐겨찾기 콤보
        RefreshFavorites();

        // 마지막 선택값 복원
        ApplyPreset("3M");      // 저장된 기간이 없을 때의 기본값
        RestoreState();

        UpdateSourceNote();

        // ── 트레이 상주 + 보유 종목 모니터링 ──
        _slack = new SlackNotifier(_config);
        _ladder = new LadderAlertEngine(_config, _registry, _slack);
        _ladder.Raised += OnLadderAlert;
        _monitor = new PortfolioMonitor(_config, _registry, _slack, _ladder);
        _monitor.AlertRaised += OnAlertRaised;
        _monitor.FetchFailed += (code, name, reason, fails) => OnFetchFailed(
            string.IsNullOrEmpty(name) ? code : $"{name} ({code})", "보유 종목", reason, fails);
        _monitor.FetchRecovered += (code, name) => OnFetchRecovered(
            string.IsNullOrEmpty(name) ? code : $"{name} ({code})", "보유 종목");
        _minuteSignal = new MinuteSignalEngine(_config, _registry, _slack);
        _minuteSignal.Raised += OnMinuteSignal;
        _reversal = new ReversalEstimator(_config, _registry);
        _watch = new WatchlistMonitor(_config, _registry, _slack, _ladder, _reversal, _minuteSignal);
        _watch.WatchAlertRaised += OnWatchAlertRaised;
        _watch.StartupSummary += OnWatchStartupSummary;
        _watch.DigestReady += OnWatchDigest;
        _watch.SessionSummaryReady += OnSessionSummary;
        _watch.ProxyLeadRaised += OnProxyLead;
        _watch.TrendPulseRaised += OnTrendPulse;
        _schedule = new MarketScheduleNotifier(_config, _slack);
        _schedule.Raised += OnScheduleAlert;
        _watch.FetchFailed += (item, reason, fails) => OnFetchFailed(item.ToString(), "관심 종목", reason, fails);
        _watch.FetchRecovered += item => OnFetchRecovered(item.ToString(), "관심 종목");
        _tray = new TrayManager();
        _tray.OpenRequested += ShowFromTray;
        _tray.ToggleMonitorRequested += ToggleMonitor;
        _tray.SettingsRequested += () => Dispatcher.Invoke(() => Settings_Click(this, new RoutedEventArgs()));
        _tray.ExitRequested += ExitApp;

        Loaded += (_, _) =>
        {
            if (_config.MonitorEnabled) StartMonitor();
            if (_config.WatchEnabled) _watch.Start();
            if (_config.MarketScheduleAlerts) _schedule.Start();
            _tray.ShowBalloon("Stock.Catch", _config.MonitorEnabled
                ? "보유 종목 모니터링 중입니다. 창을 닫아도 트레이에서 계속 실행됩니다."
                : "트레이에 상주합니다. 설정에서 모니터링을 켜면 보유 종목을 감시합니다.");
        };
    }

    private void OnWatchAlertRaised(Models.WatchAlert a) => Dispatcher.Invoke(() =>
    {
        string arrow = a.IsUp ? "▲" : "▼";
        // 시작 알림은 OnWatchStartupSummary에서 요약 1건으로 처리. 여기서는 개별 추세 알림만.
        string trend = a.IsUp ? "상승세" : "하락세";
        string body = $"현재 {a.CurrentRate:+0.0;-0.0;0.0}% (기준 {a.RefRate:+0.0;-0.0;0.0}%) · 현재가 {a.PriceText}";
        if (a.ReversalProb is { } rp)
            body += $"\n🔄 {a.ReversalDirText} 추정 ~{rp:P0} ({a.ReversalText}·{a.ReversalBasis})";
        _tray.ShowBalloon(
            $"⭐ {a.Item} {arrow} {trend} {a.Delta:+0.0;-0.0}%p ({a.WindowMinutes:0.#}분/{a.Step:0.###}%)",
            body, warning: !a.IsUp);
    });

    private void OnWatchStartupSummary(IReadOnlyList<Models.WatchAlert> alerts) => Dispatcher.Invoke(() =>
    {
        const int max = 6;
        string body = string.Join("\n", alerts.Take(max).Select(a =>
            $"{a.Item} {(a.CurrentRate >= 0 ? "▲" : "▼")} {a.CurrentRate:+0.0;-0.0;0.0}% · {a.PriceText}"));
        if (alerts.Count > max) body += $"\n…외 {alerts.Count - max}종목";
        _tray.ShowBalloon($"⭐ 관심 종목 모니터링 시작 ({alerts.Count}종목)", body);
    });

    private void OnScheduleAlert(string title, string detail) => Dispatcher.Invoke(() =>
        _tray.ShowBalloon("🔔 " + title, detail));

    private void OnProxyLead(string title, string body, bool up) => Dispatcher.Invoke(() =>
        _tray.ShowBalloon(title, body, warning: !up));

    private void OnTrendPulse(Models.WatchItem item, Services.TrendPulse p) => Dispatcher.Invoke(() =>
    {
        string display = string.IsNullOrEmpty(item.Name) ? item.Symbol : $"{item.Name} ({item.Symbol})";
        string title = p.IsFlip
            ? $"🔄 {display} {(p.Up ? "하락→상승" : "상승→하락")} 전환"
            : $"⏱ {display} {p.Milestone}분째 {(p.Up ? "상승" : "하락")}";
        string body = p.IsFlip
            ? $"직전 {(p.Up ? "하락" : "상승")} {p.PrevRunMinutes:0}분 {p.PrevRunPct:+0.0;-0.0}%  ·  {p.Horizons}"
            : $"{p.RunPct:+0.0;-0.0}%  ·  {p.Horizons}";
        _tray.ShowBalloon(title, body, warning: !p.Up);
    });

    private void OnLadderAlert(Models.LadderAlert a) => Dispatcher.Invoke(() =>
    {
        string head = a.Kind switch
        {
            Models.LadderAlertKind.BuyTouch => "🟦 매수 호가 도달",
            Models.LadderAlertKind.SellBreak => "🟥 익절가 돌파",
            _ => "⚠ 갭다운 취소선",
        };
        _tray.ShowBalloon($"{head} · {a.Display}", a.Detail,
            warning: a.Kind == Models.LadderAlertKind.GapDown);
    });

    private void OnMinuteSignal(Models.MinuteSignal s) => Dispatcher.Invoke(() =>
    {
        string head = s.Kind switch
        {
            Models.MinuteSignalKind.MorningBrief => "☀ 개장 브리핑",
            Models.MinuteSignalKind.Rebound => "📈 바닥 반등 시그널",
            Models.MinuteSignalKind.FollowThrough => "↗ 반등 지속 (직후 양봉)",
            Models.MinuteSignalKind.GoldenCross => "✅ 반등 확인 (골든크로스)",
            Models.MinuteSignalKind.StrongGoldenCross => "🔥 강력 확인 (골든크로스)",
            Models.MinuteSignalKind.HoldConfirm => "🚀 진입 적기 (추세 지속 확인)",
            Models.MinuteSignalKind.BoxBreakout => "📦 진입 권장 (박스 상단 돌파)",
            Models.MinuteSignalKind.WeakGoldenCross => "⚠ 약한 확인 (횡보성 크로스)",
            Models.MinuteSignalKind.TopWarn => "📉 고점 경고 시그널",
            Models.MinuteSignalKind.CrossTurn => "🔁 전환 확인 (교차)",
            _ => "🔻 하락 확인 (데드크로스)",
        };
        // 풍선도 Slack과 동일하게 종합 판정 우선(즉답형) — 지표 상세는 시그널 로그·분석 창에서.
        string body = s.Kind == Models.MinuteSignalKind.MorningBrief ? s.Detail : s.VerdictLine;
        if (s.StopLossPrice > 0) body += $"\n🛑 손절선: {s.StopLossPrice:N0}원 (−{s.StopLossPct:0.#}%)";
        _tray.ShowBalloon($"{s.TfLabel}{head} · {s.Display}", body, warning: s.IsBearish);
        AppendSignalLog(s);
    });

    /// <summary>
    /// 라이브 시그널 로그: 알림 1건마다 일자별 CSV에 전체 상세(판정·근거·컨텍스트)를 append.
    /// Slack 알림은 2줄 즉답형이라, 매수 후 상세 분석은 이 로그에서 본다 —
    /// 경로: %LocalAppData%\Playground\Stock.Catch\signals\yyyyMMdd_시그널로그.csv (트레이 메뉴에서 열기).
    /// </summary>
    private static void AppendSignalLog(Models.MinuteSignal s)
    {
        try
        {
            string dir = System.IO.Path.Combine(Services.AppConfig.ConfigDir, "signals");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, $"{s.Time:yyyyMMdd}_시그널로그.csv");
            bool fresh = !System.IO.File.Exists(path);
            static string Q(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
            // 판정 = 종류 라벨 + 2번째 줄(알림은 종류를 타이틀에 두지만, 로그는 한 칸에 자립적으로 담는다).
            string verdict = s.Kind == Models.MinuteSignalKind.MorningBrief
                ? s.VerdictLine : $"{SignalLabel(s.Kind)} · {s.VerdictLine}";
            var line = string.Join(",",
                s.Time.ToString("HH:mm:ss"), Q(s.Display), $"{s.Timeframe}분", Q(verdict),
                s.Price.ToString("0.####"), Q(s.Detail), Q(s.Context));
            System.IO.File.AppendAllText(path,
                (fresh ? "time,종목,tf,판정,price,근거 상세,컨텍스트\r\n" : "") + line + "\r\n",
                new System.Text.UTF8Encoding(true));
        }
        catch { /* 로그 실패가 알림을 막지 않도록 무시 */ }
    }

    private void OnFetchFailed(string display, string context, string reason, int fails) => Dispatcher.Invoke(() =>
        _tray.ShowBalloon($"⚠ {display} 시세 조회 실패 (연속 {fails}회)", $"{context} · {reason}", warning: true));

    private void OnFetchRecovered(string display, string context) => Dispatcher.Invoke(() =>
        _tray.ShowBalloon($"✓ {display} 시세 조회 복구", $"{context} · 정상 수신 중"));

    private void OnSessionSummary(bool isPre, string title, string body) => Dispatcher.Invoke(() =>
        _tray.ShowBalloon(title, body));

    private void OnWatchDigest(IReadOnlyList<WatchQuote> quotes) => Dispatcher.Invoke(() =>
    {
        string body = string.Join("\n", quotes.Take(6).Select(q =>
            $"{q.Item} {(q.ChangeRate >= 0 ? "▲" : "▼")} {q.ChangeRate:+0.0;-0.0;0.0}%"));
        _tray.ShowBalloon($"⭐ 관심 종목 시세 ({quotes.Count}종목)", body);
    });

    // ───────────────────────── 트레이/모니터링 ─────────────────────────

    private void OnAlertRaised(Models.PortfolioAlert a) => Dispatcher.Invoke(() =>
    {
        string arrow = a.IsUp ? "▲" : "▼";
        _tray.ShowBalloon(
            $"{a.Display} {arrow} {a.ReturnPct:+0.0;-0.0}%",
            $"현재가 {a.Price:N0}원 · 평단 {a.AvgPrice:N0}원 · 평가손익 {a.EvalPL:+#,0;-#,0;0}원",
            warning: !a.IsUp);
    });

    private void StartMonitor()
    {
        _monitor.Start();
        _tray.SetMonitorState(true);
    }

    private void ToggleMonitor() => Dispatcher.Invoke(() =>
    {
        if (_monitor.IsRunning) { _monitor.Stop(); _config.MonitorEnabled = false; _tray.SetMonitorState(false); }
        else { _config.MonitorEnabled = true; StartMonitor(); }
        _config.Save();
    });

    private void ShowFromTray() => Dispatcher.Invoke(() =>
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true; Topmost = false;
    });

    private void ExitApp() => Dispatcher.Invoke(() =>
    {
        _reallyExit = true;
        Close();
    });

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // X 버튼/Alt+F4 → 종료가 아니라 트레이로 숨김(상주). 종료는 트레이 메뉴 '종료'로만.
        if (!_reallyExit)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloon("Stock.Catch", "트레이에서 계속 실행 중입니다. 완전히 끄려면 트레이 아이콘 우클릭 → 종료.");
            }
            return;
        }
        SaveState();
        _monitor.Dispose();
        _watch.Dispose();
        _schedule.Dispose();
        _slack.Dispose();
        _tray.Dispose();
        _registry.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }

    // ────────────────────────────── 상태 저장/복원 ──────────────────────────────
    private void RestoreState()
    {
        CodeBox.Text = string.IsNullOrWhiteSpace(_config.LastCode) ? "005930" : _config.LastCode;
        NameText.Text = _config.LastName;
        if (TryParseDate(_config.LastFrom, out _)) FromBox.Text = _config.LastFrom;
        if (TryParseDate(_config.LastTo, out _)) ToBox.Text = _config.LastTo;

        SourceCombo.SelectedItem = _registry.Get(_config.LastSource);
        FormatCombo.SelectedItem = FormatCombo.Items.Cast<FormatItem>()
            .FirstOrDefault(f => f.Format == _config.LastFormat) ?? FormatCombo.Items[0];

        // 컬럼 선택(빈 목록이면 전체)
        var cols = _config.LastColumns.Count > 0 ? _config.LastColumns.ToHashSet() : null;
        ColDate.IsChecked = cols?.Contains(CandleColumn.Date) ?? true;
        ColOpen.IsChecked = cols?.Contains(CandleColumn.Open) ?? true;
        ColClose.IsChecked = cols?.Contains(CandleColumn.Close) ?? true;
        ColLow.IsChecked = cols?.Contains(CandleColumn.Low) ?? true;
        ColHigh.IsChecked = cols?.Contains(CandleColumn.High) ?? true;
        ColVolume.IsChecked = cols?.Contains(CandleColumn.Volume) ?? true;
        IncludeHeaderCheck.IsChecked = _config.LastIncludeHeader;
    }

    private void SaveState()
    {
        _config.LastCode = CodeBox.Text.Trim();
        _config.LastName = NameText.Text;
        _config.LastFrom = FromBox.Text.Trim();
        _config.LastTo = ToBox.Text.Trim();
        if (SourceCombo.SelectedItem is IPriceSource src) _config.LastSource = src.Kind;
        if (FormatCombo.SelectedItem is FormatItem fi) _config.LastFormat = fi.Format;
        _config.LastColumns = SelectedColumns();
        _config.LastIncludeHeader = IncludeHeaderCheck.IsChecked == true;
        _config.Save();
    }

    // ────────────────────────────── 기간 프리셋 ──────────────────────────────
    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag) ApplyPreset(tag);
    }

    private void ApplyPreset(string tag)
    {
        var to = DateTime.Today;
        var from = tag switch
        {
            "1M" => to.AddMonths(-1),
            "3M" => to.AddMonths(-3),
            "6M" => to.AddMonths(-6),
            "1Y" => to.AddYears(-1),
            "3Y" => to.AddYears(-3),
            "YTD" => new DateTime(to.Year, 1, 1),
            _ => to.AddMonths(-3)
        };
        FromBox.Text = from.ToString("yyyy-MM-dd");
        ToBox.Text = to.ToString("yyyy-MM-dd");
    }

    /// <summary>현재 입력된 기간(시작·종료) 전체를 ±N일 이동.</summary>
    private void DayShift_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag || !int.TryParse(tag, out int d)) return;
        if (!TryParseDate(FromBox.Text, out var from) || !TryParseDate(ToBox.Text, out var to))
        {
            ShowError("기간은 yyyy-MM-dd 형식으로 입력하세요.");
            return;
        }
        FromBox.Text = from.AddDays(d).ToString("yyyy-MM-dd");
        ToBox.Text = to.AddDays(d).ToString("yyyy-MM-dd");
    }

    // ────────────────────────────── 종목 검색 (코드 또는 이름) ──────────────────────────────
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        string text = CodeBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowError("종목코드 또는 이름을 입력하세요(예: 005930 / 삼성전자).");
            return;
        }
        LookupBtn.IsEnabled = false;
        NameText.Text = "검색 중…";
        try
        {
            var hits = await _registry.SearchAsync(text);
            if (hits.Count == 0) { NameText.Text = "(검색 결과 없음)"; return; }

            // 1건이면 바로 적용, 여러 건이면 선택 다이얼로그.
            StockHit? pick = hits.Count == 1 ? hits[0] : null;
            if (pick is null)
            {
                var win = new SearchResultWindow(text, hits) { Owner = this };
                pick = win.ShowDialog() == true ? win.Selected : null;
            }

            if (pick != null)
            {
                CodeBox.Text = pick.Code;
                NameText.Text = pick.Name;
            }
            else if (NameText.Text == "검색 중…")
            {
                NameText.Text = ""; // 취소
            }
        }
        finally
        {
            LookupBtn.IsEnabled = true;
        }
    }

    // ────────────────────────────── 즐겨찾기 ──────────────────────────────
    private void RefreshFavorites()
    {
        FavCombo.ItemsSource = null;
        FavCombo.ItemsSource = _config.Favorites;
    }

    private void Fav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavCombo.SelectedItem is FavoriteStock f)
        {
            CodeBox.Text = f.Code;
            NameText.Text = f.Name;
        }
    }

    /// <summary>
    /// 국내 단축코드 검증·정규화: 5~6자리 영숫자(2025년~ 신형 코드 0193T0 등 문자 포함 허용) → 대문자.
    /// 실패 시 오류 표시 후 false.
    /// </summary>
    private bool TryGetKrCode(out string code)
    {
        code = CodeBox.Text.Trim().ToUpperInvariant();
        if (code.Length is >= 5 and <= 6 && code.All(char.IsLetterOrDigit)) return true;
        ShowError("종목코드는 5~6자리 영숫자입니다(예: 005930, 0193T0).");
        return false;
    }

    private void FavAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKrCode(out var code)) return;
        // 미조회/실패 표시는 이름으로 저장하지 않음
        string name = NameText.Text is "조회 중…" or "(이름 못 찾음)" ? "" : NameText.Text.Trim();

        var existing = _config.Favorites.FirstOrDefault(f => f.Code == code);
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(name)) existing.Name = name;
        }
        else
        {
            _config.Favorites.Add(new FavoriteStock { Code = code, Name = name });
        }
        _config.Save();
        RefreshFavorites();
        FavCombo.SelectedItem = _config.Favorites.FirstOrDefault(f => f.Code == code);
        SummaryText.Text = $"⭐ 즐겨찾기 추가: {code}{(string.IsNullOrEmpty(name) ? "" : "  " + name)}";
    }

    private void FavRename_Click(object sender, RoutedEventArgs e)
    {
        if (FavCombo.SelectedItem is not FavoriteStock f)
        {
            ShowError("이름을 수정할 즐겨찾기를 콤보에서 선택하세요.");
            return;
        }
        var win = new FavoriteEditWindow(f) { Owner = this };
        if (win.ShowDialog() != true) return;
        _config.Save();
        RefreshFavorites();
        FavCombo.SelectedItem = _config.Favorites.FirstOrDefault(x => x.Code == f.Code);
        SummaryText.Text = $"✏ 즐겨찾기 이름 수정: {f.Code}  {f.Name}";
    }

    private void FavRemove_Click(object sender, RoutedEventArgs e)
    {
        if (FavCombo.SelectedItem is not FavoriteStock f)
        {
            ShowError("제거할 즐겨찾기를 콤보에서 선택하세요.");
            return;
        }
        _config.Favorites.RemoveAll(x => x.Code == f.Code);
        _config.Save();
        RefreshFavorites();
        SummaryText.Text = $"🗑 즐겨찾기 제거: {f.Code}  {f.Name}";
    }

    private void FavUp_Click(object sender, RoutedEventArgs e) => MoveFavorite(-1);
    private void FavDown_Click(object sender, RoutedEventArgs e) => MoveFavorite(+1);

    /// <summary>선택된 즐겨찾기를 한 칸 위(-1)/아래(+1)로 이동하고 저장.</summary>
    private void MoveFavorite(int dir)
    {
        if (FavCombo.SelectedItem is not FavoriteStock f)
        {
            ShowError("순서를 바꿀 즐겨찾기를 콤보에서 선택하세요.");
            return;
        }
        int i = _config.Favorites.IndexOf(f);
        int j = i + dir;
        if (i < 0 || j < 0 || j >= _config.Favorites.Count) return;
        (_config.Favorites[i], _config.Favorites[j]) = (_config.Favorites[j], _config.Favorites[i]);
        _config.Save();
        RefreshFavorites();
        FavCombo.SelectedItem = _config.Favorites.FirstOrDefault(x => x.Code == f.Code);
    }

    // ────────────────────────────── 조회 ──────────────────────────────
    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKrCode(out var code)) return;
        if (!TryParseDate(FromBox.Text, out var from) || !TryParseDate(ToBox.Text, out var to))
        {
            ShowError("기간은 yyyy-MM-dd 형식으로 입력하세요.");
            return;
        }
        if (from > to)
        {
            ShowError("시작일이 종료일보다 늦습니다.");
            return;
        }
        if (SourceCombo.SelectedItem is not IPriceSource source) return;

        if (source.RequiresApiKey && !_config.HasKisCredentials)
        {
            ShowError("KIS 소스는 API 키가 필요합니다. [⚙ KIS 키 설정]에서 입력하세요.");
            return;
        }

        SetBusy(true, $"{source.DisplayName}에서 조회 중…");
        try
        {
            var series = await source.FetchAsync(code, from, to);
            _series = series;
            Grid.ItemsSource = series.Candles;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = LadderBtn.IsEnabled = series.Candles.Count > 0;
            UpdateSummary(series);

            // 종목명 자동 표시: 소스가 주면 사용, 아니면 KRX finder로 조회
            if (!string.IsNullOrEmpty(series.Name)) NameText.Text = series.Name;
            else if (string.IsNullOrEmpty(NameText.Text) || NameText.Text.StartsWith('('))
            {
                var nm = await _registry.LookupNameAsync(code);
                if (!string.IsNullOrEmpty(nm)) NameText.Text = nm;
            }

            // 최근 사용값 저장
            _config.LastSource = source.Kind;
            _config.LastCode = code;
            _config.Save();
        }
        catch (PriceSourceException ex)
        {
            _series = null;
            Grid.ItemsSource = null;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = LadderBtn.IsEnabled = false;
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _series = null;
            Grid.ItemsSource = null;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = LadderBtn.IsEnabled = false;
            ShowError($"조회 중 오류: {ex.Message}");
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void UpdateSummary(StockSeries s)
    {
        if (s.Candles.Count == 0) { SummaryText.Text = "데이터가 없습니다."; return; }
        decimal hi = s.Candles.Max(c => c.High);
        decimal lo = s.Candles.Min(c => c.Low);
        string name = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Name} ({s.Code})";
        string market = string.IsNullOrEmpty(s.Market) ? "" : $" · {s.Market}";
        SummaryText.Text =
            $"{name}{market}  |  {s.SourceLabel}  |  {s.Candles[0].Date:yyyy-MM-dd} ~ {s.Candles[^1].Date:yyyy-MM-dd}  " +
            $"|  {s.Candles.Count}건  |  기간 고가 {hi:N0} · 저가 {lo:N0}";
    }

    // ────────────────────────────── 내보내기 ──────────────────────────────
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null || FormatCombo.SelectedItem is not FormatItem fi) return;
        var cols = SelectedColumns();
        if (cols.Count == 0) { ShowError("내보낼 컬럼을 1개 이상 선택하세요."); return; }

        var dlg = new SaveFileDialog
        {
            Filter = DataExporter.FilterLabel(fi.Format) + "|모든 파일|*.*",
            FileName = BuildFileName(_series, fi.Format),
            InitialDirectory = Directory.Exists(_config.LastExportDir) ? _config.LastExportDir : null
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            await DataExporter.SaveAsync(_series, fi.Format, cols, IncludeHeaderCheck.IsChecked == true, dlg.FileName);
            _config.LastExportDir = Path.GetDirectoryName(dlg.FileName) ?? "";
            _config.Save();
            SummaryText.Text = $"저장 완료({cols.Count}개 컬럼): {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError($"저장 실패: {ex.Message}");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null || FormatCombo.SelectedItem is not FormatItem fi) return;
        var cols = SelectedColumns();
        if (cols.Count == 0) { ShowError("복사할 컬럼을 1개 이상 선택하세요."); return; }
        try
        {
            Clipboard.SetText(DataExporter.Serialize(_series, fi.Format, cols, IncludeHeaderCheck.IsChecked == true));
            SummaryText.Text = $"클립보드에 복사됨 ({fi.Label}, {cols.Count}개 컬럼 · {_series.Candles.Count}건).";
        }
        catch (Exception ex)
        {
            ShowError($"복사 실패: {ex.Message}");
        }
    }

    /// <summary>체크된 컬럼을 표시 순서(날짜-시가-종가-저가-고가-거래량)대로 반환.</summary>
    private List<CandleColumn> SelectedColumns()
    {
        var list = new List<CandleColumn>();
        if (ColDate.IsChecked == true) list.Add(CandleColumn.Date);
        if (ColOpen.IsChecked == true) list.Add(CandleColumn.Open);
        if (ColClose.IsChecked == true) list.Add(CandleColumn.Close);
        if (ColLow.IsChecked == true) list.Add(CandleColumn.Low);
        if (ColHigh.IsChecked == true) list.Add(CandleColumn.High);
        if (ColVolume.IsChecked == true) list.Add(CandleColumn.Volume);
        return list;
    }

    private void ColAll_Click(object sender, RoutedEventArgs e) => SetAllColumns(true);
    private void ColNone_Click(object sender, RoutedEventArgs e) => SetAllColumns(false);

    private void SetAllColumns(bool on)
    {
        ColDate.IsChecked = ColOpen.IsChecked = ColClose.IsChecked =
            ColLow.IsChecked = ColHigh.IsChecked = ColVolume.IsChecked = on;
    }

    private static string BuildFileName(StockSeries s, ExportFormat fmt)
    {
        string namePart = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Code}_{s.Name}";
        foreach (char c in Path.GetInvalidFileNameChars()) namePart = namePart.Replace(c, '_');
        string range = s.Candles.Count > 0
            ? $"_{s.Candles[0].Date:yyyyMMdd}-{s.Candles[^1].Date:yyyyMMdd}"
            : "";
        return $"{namePart}{range}{DataExporter.Extension(fmt)}";
    }

    // ────────────────────────────── 차트 ──────────────────────────────
    private void Chart_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKrCode(out var code)) return;
        new ChartWindow(code, NameText.Text is "조회 중…" or "(이름 못 찾음)" or "(검색 결과 없음)" ? "" : NameText.Text, _registry, _config)
        { Owner = this }.Show();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
        => new SignalHelpWindow { Owner = this }.Show();

    // ────────────────────────────── 분봉 CSV ──────────────────────────────
    /// <summary>
    /// 기간 내 영업일마다 1분봉을 KIS 일별분봉으로 조회해 선택한 폴더에 일자별 CSV로 저장한다
    /// (주말·휴장일 자동 건너뜀 · 파일명 "이름(코드)_yyyyMMdd_1분봉.csv").
    /// </summary>
    private async void MinuteCsv_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKrCode(out var code)) return;
        if (!_config.HasKisCredentials)
        {
            ShowError("분봉 조회에는 KIS API 키가 필요합니다. [⚙ KIS 키 설정]에서 입력하세요.");
            return;
        }

        // 메인 창의 조회 기간을 초기값으로 전달(파싱 실패 시 기본값 — 최근 영업일 하루)
        var pick = new MinuteCsvWindow(
            TryParseDate(FromBox.Text, out var mf) ? mf : null,
            TryParseDate(ToBox.Text, out var mt) ? mt : null) { Owner = this };
        if (pick.ShowDialog() != true) return;

        var folder = new OpenFolderDialog
        {
            Title = "분봉 CSV 저장 폴더 선택",
            InitialDirectory = Directory.Exists(_config.LastExportDir) ? _config.LastExportDir : null
        };
        if (folder.ShowDialog(this) != true) return;
        string outDir = folder.FolderName;
        _config.LastExportDir = outDir;
        _config.Save();

        string name = NameText.Text is "조회 중…" or "(이름 못 찾음)" or "(검색 결과 없음)" ? "" : NameText.Text.Trim();
        string stem = string.IsNullOrEmpty(name) ? code : $"{name}({code})";

        MinuteCsvBtn.IsEnabled = false;
        int ok = 0, skip = 0;
        string lastFile = "";
        try
        {
            for (var date = pick.FromDate; date <= pick.ToDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                SummaryText.Text = $"🕐 {date:yyyy-MM-dd} 1분봉 조회 중… ({code} · 완료 {ok}건)";
                try
                {
                    var bars = await _registry.KisDayMinutesAsync(code, date);

                    // 기존 내보내기 컬럼 순서(날짜-시가-종가-저가-고가-거래량)에 시각(time)만 추가.
                    var sb = new System.Text.StringBuilder();
                    sb.Append("date,time,open,close,low,high,volume\n");
                    foreach (var b in bars)
                        sb.Append($"{b.Date:yyyy-MM-dd},{b.Date:HH:mm},{Num(b.Open)},{Num(b.Close)},{Num(b.Low)},{Num(b.High)},{b.Volume}\n");

                    lastFile = Path.Combine(outDir, $"{stem}_{date:yyyyMMdd}_1분봉.csv");
                    await File.WriteAllTextAsync(lastFile, sb.ToString(),
                        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));   // 엑셀 한글 호환 BOM
                    ok++;
                }
                catch
                {
                    skip++;   // 휴장일/보관 기간 초과 등 — 해당 일자만 건너뛰고 계속
                }
                await Task.Delay(300);   // KIS 유량 완화
            }

            SummaryText.Text = pick.FromDate == pick.ToDate && ok == 1
                ? $"🕐 분봉 CSV 저장 완료: {pick.FromDate:yyyy-MM-dd} → {lastFile}"
                : $"🕐 분봉 CSV {ok}일치 저장 완료{(skip > 0 ? $" · 건너뜀 {skip}일(휴장/데이터 없음)" : "")} → {outDir}";
            if (ok == 0) ShowError("저장된 파일이 없습니다(휴장일이거나 KIS 보관 기간을 벗어난 기간일 수 있습니다).");
        }
        finally
        {
            MinuteCsvBtn.IsEnabled = true;
        }

        // 불필요한 소수점 0 제거(예: 53000, 53.34) — ETF/ETN 정수가 아닌 가격 대비.
        static string Num(decimal d) => d == Math.Truncate(d)
            ? ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ────────────────────────────── 분봉 시그널 백테스트 ──────────────────────────────
    /// <summary>
    /// 저장한 1분봉 CSV(다중 선택 가능)를 열어 바닥 반등·고점 경고 시그널(골든/데드크로스 확인 포함)이
    /// 발생했을 시점을 라이브 엔진과 동일 조건으로 추출해 파일별로 "원본명_시그널.csv"로 저장한다.
    /// 반대 짝(레버리지↔인버스) CSV를 함께 지정하면 라이브와 동일하게 🔁 전환 확인(교차)까지 포함한다.
    /// </summary>
    private async void SignalCsv_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog
        {
            Title = "① 분봉 CSV 선택 — 여러 파일 선택 가능 (date,time,open,close,low,high,volume)",
            Filter = "CSV (쉼표 구분)|*.csv|모든 파일|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(_config.LastSignalDir) ? _config.LastSignalDir
                : Directory.Exists(_config.LastExportDir) ? _config.LastExportDir : null
        };
        if (open.ShowDialog(this) != true || open.FileNames.Length == 0) return;

        // ② 반대 짝(레버리지↔인버스) CSV로 교차 알림까지 분석할지 확인.
        string[]? pairFiles = null;
        var wantPair = MessageBox.Show(
            "반대 짝(레버리지↔인버스) 분봉도 함께 분석해 🔁 전환 확인(교차) 알림을 포함할까요?\n\n" +
            "예: 방금 고른 파일들과 같은 날짜의 반대 종목 CSV를 이어서 선택합니다(날짜 쌍이 맞아야 함).\n" +
            "아니오: 종목별 개별 분석(기존 방식).",
            "🔁 전환 확인 (교차) 포함", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (wantPair == MessageBoxResult.Cancel) return;

        if (wantPair == MessageBoxResult.Yes)
        {
            // ③ 반대 짝 CSV 리스트 선택 + 날짜 쌍 검증.
            var openPair = new OpenFileDialog
            {
                Title = "③ 반대 짝 분봉 CSV 선택 (같은 날짜 · 같은 개수)",
                Filter = "CSV (쉼표 구분)|*.csv|모든 파일|*.*",
                Multiselect = true,
                InitialDirectory = Path.GetDirectoryName(open.FileNames[0])
            };
            if (openPair.ShowDialog(this) != true || openPair.FileNames.Length == 0) return;

            string? err = ValidatePairDates(open.FileNames, openPair.FileNames);
            if (err != null)
            {
                MessageBox.Show(err, "짝 데이터 날짜 검증 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            pairFiles = openPair.FileNames;
        }

        // ④ 결과(_시그널.csv) 저장 폴더 선택.
        string srcDir = Path.GetDirectoryName(open.FileNames[0]) ?? "";
        var folder = new OpenFolderDialog
        {
            Title = "④ 시그널 결과(_시그널.csv) 저장 폴더 선택",
            InitialDirectory = Directory.Exists(_config.LastSignalOutDir) ? _config.LastSignalOutDir : srcDir
        };
        if (folder.ShowDialog(this) != true) return;
        string outDir = folder.FolderName;

        _config.LastSignalDir = srcDir;
        _config.LastSignalOutDir = outDir;
        _config.Save();

        SignalCsvBtn.IsEnabled = false;
        try
        {
            int okFiles = 0, failFiles = 0, totalSignals = 0, crossCount = 0;
            var parts = new List<string>();
            var collected = new List<SignalResultWindow.FileSignals>();

            // ⑤ 처리: 짝이 있으면 날짜별 페어로 교차 포함, 없으면 종목별 개별.
            if (pairFiles != null)
            {
                var mainByDate = MapByDate(open.FileNames);
                var pairByDate = MapByDate(pairFiles);
                foreach (var (date, mainFile) in mainByDate.OrderBy(kv => kv.Key))
                {
                    string stemA = Path.GetFileNameWithoutExtension(mainFile);
                    string stemB = Path.GetFileNameWithoutExtension(pairByDate[date]);
                    try
                    {
                        var barsA = ParseMinuteCsv(mainFile);
                        var barsB = ParseMinuteCsv(pairByDate[date]);
                        if (barsA.Count < 26 || barsB.Count < 26)
                            throw new InvalidDataException($"분봉 부족(A {barsA.Count}·B {barsB.Count}봉)");

                        var (codeA, nameA) = CodeNameOf(stemA);
                        var (codeB, nameB) = CodeNameOf(stemB);
                        var (trendA, prevA) = _config.BottomTrendGate ? await TryDayTrendAsync(stemA) : (null, 0m);
                        var (trendB, prevB) = _config.BottomTrendGate ? await TryDayTrendAsync(stemB) : (null, 0m);

                        // 종목별 시그널 override를 라이브와 동일하게 적용(관심 종목에 있으면 그 설정).
                        var sigA = _minuteSignal.Backtest(barsA, ResolveWatchItem(codeA, nameA), trendA, prevA);
                        var sigB = _minuteSignal.Backtest(barsB, ResolveWatchItem(codeB, nameB), trendB, prevB);
                        var cross = MinuteSignalEngine.DetectCrossTurns(sigA, barsA, codeA, nameA, sigB, barsB, codeB, nameB);
                        crossCount += cross.Count;

                        // 교차는 발화 종목(Code) 파일에 귀속.
                        var listA = sigA.Concat(cross.Where(c => c.Code == codeA)).OrderBy(s => s.Time).ThenBy(s => s.Timeframe).ToList();
                        var listB = sigB.Concat(cross.Where(c => c.Code == codeB)).OrderBy(s => s.Time).ThenBy(s => s.Timeframe).ToList();

                        await WriteSignalCsvAsync(Path.Combine(outDir, stemA + "_시그널.csv"), listA);
                        await WriteSignalCsvAsync(Path.Combine(outDir, stemB + "_시그널.csv"), listB);
                        collected.Add(new SignalResultWindow.FileSignals(stemA, listA));
                        collected.Add(new SignalResultWindow.FileSignals(stemB, listB));
                        okFiles += 2;
                        totalSignals += listA.Count + listB.Count;
                        parts.Add($"{date:MM-dd} 짝 {listA.Count + listB.Count}건" + (cross.Count > 0 ? $"(🔁{cross.Count})" : ""));
                    }
                    catch (Exception ex)
                    {
                        failFiles += 2;
                        parts.Add($"{date:MM-dd} 실패({ex.Message})");
                    }
                }
            }
            else
            {
                foreach (var file in open.FileNames)
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var bars = ParseMinuteCsv(file);
                        if (bars.Count < 26)
                            throw new InvalidDataException($"분봉 부족({bars.Count}봉, 최소 26봉)");
                        var (dayTrend, prevClose) = _config.BottomTrendGate ? await TryDayTrendAsync(stem) : (null, 0m);
                        var (code, name) = CodeNameOf(stem);
                        var signals = _minuteSignal.Backtest(bars, ResolveWatchItem(code, name), dayTrend, prevClose);

                        await WriteSignalCsvAsync(Path.Combine(outDir, stem + "_시그널.csv"), signals);
                        okFiles++;
                        totalSignals += signals.Count;
                        parts.Add($"{stem} {signals.Count}건");
                        collected.Add(new SignalResultWindow.FileSignals(stem, signals));
                    }
                    catch (Exception ex)
                    {
                        failFiles++;
                        parts.Add($"{stem} 실패({ex.Message})");
                    }
                }
            }

            string detail = string.Join(" · ", parts.Take(4)) + (parts.Count > 4 ? $" · 외 {parts.Count - 4}개" : "");
            SummaryText.Text = $"🧪 {okFiles}개 파일 · 시그널 총 {totalSignals}건" +
                (crossCount > 0 ? $" · 🔁 전환 확인 {crossCount}건" : "") +
                (failFiles > 0 ? $" · 실패 {failFiles}" : "") + $" — {detail}";

            if (collected.Count > 0 && totalSignals > 0)
                new SignalResultWindow(collected, _slack) { Owner = this }.Show();
        }
        finally
        {
            SignalCsvBtn.IsEnabled = true;
        }
    }

    /// <summary>코드로 관심 종목을 찾아 반환(종목별 시그널 override 포함). 없으면 전역 설정용 기본 WatchItem.</summary>
    private Models.WatchItem ResolveWatchItem(string code, string name)
        => _config.Watchlist.FirstOrDefault(w => string.Equals(w.Symbol, code, StringComparison.OrdinalIgnoreCase))
           ?? new Models.WatchItem { Symbol = code, Name = name };

    /// <summary>파일 stem "이름(코드)_yyyyMMdd_..."에서 코드·이름 추출. 이름은 관심 종목에 있으면 그 이름(짧음) 우선.</summary>
    private (string Code, string Name) CodeNameOf(string stem)
    {
        var m = System.Text.RegularExpressions.Regex.Match(stem, @"^(.*)\(([0-9A-Za-z]{5,6})\)_\d{8}_");
        if (!m.Success) return (stem, "");
        string code = m.Groups[2].Value.ToUpperInvariant();
        string nameRaw = m.Groups[1].Value.Trim();
        string name = _config.Watchlist.FirstOrDefault(w =>
            string.Equals(w.Symbol, code, StringComparison.OrdinalIgnoreCase))?.Name ?? nameRaw;
        return (code, name);
    }

    /// <summary>파일명에서 yyyyMMdd 날짜 추출(실패 시 null).</summary>
    private static DateTime? DateOf(string path)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(path), @"_(\d{8})_");
        return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>파일 목록을 날짜→파일로 매핑(같은 날짜 중복 시 예외 던져 검증에서 걸림).</summary>
    private static Dictionary<DateTime, string> MapByDate(IEnumerable<string> files)
        => files.ToDictionary(f => DateOf(f) ?? throw new InvalidDataException($"날짜 없음: {Path.GetFileName(f)}"), f => f);

    /// <summary>주·짝 CSV 리스트의 날짜 쌍 검증 — 개수·날짜 집합이 정확히 일치해야 null(통과).</summary>
    private static string? ValidatePairDates(string[] mainFiles, string[] pairFiles)
    {
        if (mainFiles.Length != pairFiles.Length)
            return $"개수가 다릅니다 — 주 {mainFiles.Length}개 vs 짝 {pairFiles.Length}개.";
        var mainSet = new HashSet<DateTime>();
        foreach (var f in mainFiles) { var d = DateOf(f); if (d is null) return $"주 파일 날짜 인식 실패: {Path.GetFileName(f)}"; if (!mainSet.Add(d.Value)) return $"주 파일에 같은 날짜 중복: {d:yyyy-MM-dd}"; }
        var pairSet = new HashSet<DateTime>();
        foreach (var f in pairFiles) { var d = DateOf(f); if (d is null) return $"짝 파일 날짜 인식 실패: {Path.GetFileName(f)}"; if (!pairSet.Add(d.Value)) return $"짝 파일에 같은 날짜 중복: {d:yyyy-MM-dd}"; }
        if (!mainSet.SetEquals(pairSet))
        {
            var onlyMain = mainSet.Except(pairSet).OrderBy(d => d).Select(d => d.ToString("MM-dd"));
            var onlyPair = pairSet.Except(mainSet).OrderBy(d => d).Select(d => d.ToString("MM-dd"));
            return "날짜가 서로 맞지 않습니다.\n" +
                (onlyMain.Any() ? $"주에만 있음: {string.Join(", ", onlyMain)}\n" : "") +
                (onlyPair.Any() ? $"짝에만 있음: {string.Join(", ", onlyPair)}" : "");
        }
        return null;
    }

    /// <summary>시그널 목록을 "_시그널.csv"(date,time,tf,signal,price,detail,context)로 저장. UTF-8 BOM.</summary>
    private static async Task WriteSignalCsvAsync(string outPath, IEnumerable<Models.MinuteSignal> signals)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("date,time,tf,signal,price,detail,context\n");
        static string Esc(string v) => v.Replace("\"", "\"\"");
        foreach (var s in signals.OrderBy(x => x.Time).ThenBy(x => x.Timeframe))
            sb.Append($"{s.Time:yyyy-MM-dd},{s.Time:HH:mm},{s.Timeframe}분,{SignalLabel(s.Kind)}," +
                $"{s.Price.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}," +
                $"\"{Esc(s.Detail)}\",\"{Esc(s.Context)}\"\n");
        await File.WriteAllTextAsync(outPath, sb.ToString(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 백테스트용 과거 시점 일봉 컨텍스트(추세점수·전일 종가) — 파일명에서 (코드)_yyyyMMdd 추출 후
    /// 직전 완성 일봉으로 계산. 실패 시 (null, 0).
    /// </summary>
    private async Task<(double? Trend, decimal PrevClose)> TryDayTrendAsync(string stem)
    {
        try
        {
            var m = System.Text.RegularExpressions.Regex.Match(stem, @"\(([0-9A-Za-z]{5,6})\)_(\d{8})_");
            if (!m.Success) return (null, 0m);
            string code = m.Groups[1].Value.ToUpperInvariant();
            if (!DateTime.TryParseExact(m.Groups[2].Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var day))
                return (null, 0m);

            var daily = await _registry.KrDailyRangeAsync(code, day.AddDays(-70), day.AddDays(-1));
            var completed = daily.Where(c => c.Date.Date < day.Date).ToList();
            if (completed.Count < LadderCalculator.RequiredDays) return (null, 0m);
            var win = completed.TakeLast(LadderCalculator.RequiredDays).ToList();
            var r = LadderCalculator.Calculate(new StockSeries(code, "", "", SourceKind.Naver, win),
                new LadderParams(0, 0, UseTrend: true));
            return (r.TrendScore, completed[^1].Close);
        }
        catch { return (null, 0m); }
    }

    private static string SignalLabel(Models.MinuteSignalKind kind) => kind switch
    {
        Models.MinuteSignalKind.MorningBrief => "개장 브리핑",
        Models.MinuteSignalKind.Rebound => "바닥 반등",
        Models.MinuteSignalKind.FollowThrough => "반등 지속(직후 양봉)",
        Models.MinuteSignalKind.GoldenCross => "골든크로스(반등 확인)",
        Models.MinuteSignalKind.StrongGoldenCross => "골든크로스(강력)",
        Models.MinuteSignalKind.HoldConfirm => "진입 적기(추세 지속)",
        Models.MinuteSignalKind.BoxBreakout => "진입 권장(박스 상단 돌파)",
        Models.MinuteSignalKind.WeakGoldenCross => "골든크로스(약·모멘텀 부족)",
        Models.MinuteSignalKind.TopWarn => "고점 경고",
        Models.MinuteSignalKind.CrossTurn => "전환 확인(교차)",
        _ => "데드크로스(하락 확인)",
    };

    /// <summary>분봉 CSV → Candle 목록(공용 유틸 위임).</summary>
    private static List<Candle> ParseMinuteCsv(string path) => MinuteCsvIo.Parse(path);

    // ────────────────────────────── 매수/익절 래더 계산 ──────────────────────────────
    private void Ladder_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null) return;
        if (_series.Candles.Count < LadderCalculator.RequiredDays)
        {
            ShowError($"매수/익절 계산에는 최소 {LadderCalculator.RequiredDays}거래일이 필요합니다(현재 {_series.Candles.Count}일). 기간을 늘려 다시 조회하세요.");
            return;
        }
        // 모달리스 — 여러 종목 비교 가능. 창 내부에서 공격성·추세 슬라이더로 라이브 재계산.
        new LadderWindow(_series, _registry, _config) { Owner = this }.Show();
    }

    // ────────────────────────────── 내 자산(포트폴리오) ──────────────────────────────
    private void Portfolio_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        string name = NameText.Text is "조회 중…" or "검색 중…" or "(이름 못 찾음)" or "(검색 결과 없음)" ? "" : NameText.Text;
        new PortfolioWindow(_config, _registry, code, name) { Owner = this }.Show();
    }

    // ────────────────────────────── 관심 종목(워치리스트) ──────────────────────────────
    private void Watchlist_Click(object sender, RoutedEventArgs e)
    {
        var win = new WatchlistWindow(_config, _registry, _watch) { Owner = this };
        win.MonitorToggled += on => Dispatcher.Invoke(() =>
        {
            if (on && !_watch.IsRunning) _watch.Start();
            else if (!on && _watch.IsRunning) _watch.Stop();
        });
        win.Show();
    }

    // ────────────────────────────── 설정 ──────────────────────────────
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_config, _slack, _registry) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _config.Save();
            UpdateSourceNote();
            // 모니터링 설정 변경 즉시 반영
            if (_config.MonitorEnabled && !_monitor.IsRunning) StartMonitor();
            else if (!_config.MonitorEnabled && _monitor.IsRunning) { _monitor.Stop(); _tray.SetMonitorState(false); }
            // 장 세션 알림 토글 반영
            if (_config.MarketScheduleAlerts && !_schedule.IsRunning) _schedule.Start();
            else if (!_config.MarketScheduleAlerts && _schedule.IsRunning) _schedule.Stop();
        }
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSourceNote();

    private void UpdateSourceNote()
    {
        if (SourceCombo.SelectedItem is not IPriceSource src) return;
        SourceNote.Text = src.Kind switch
        {
            SourceKind.Naver => "무인증 · 가장 빠름",
            SourceKind.Yahoo => "무인증 · 글로벌(KST 보정)",
            SourceKind.Daum => "무인증 · KRX 원천 시세",
            SourceKind.Kis => _config.HasKisCredentials ? "API 키 설정됨 · 공식" : "⚠ API 키 필요 (⚙ 설정)",
            _ => ""
        };
    }

    // ────────────────────────────── 헬퍼 ──────────────────────────────
    private static bool TryParseDate(string s, out DateTime dt) =>
        DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd", null,
            System.Globalization.DateTimeStyles.None, out dt);

    private void SetBusy(bool busy, string? message)
    {
        FetchBtn.IsEnabled = !busy;
        FetchBtn.Content = busy ? "조회 중…" : "조회";
        if (message != null) SummaryText.Text = message;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private void ShowError(string message) => SummaryText.Text = "⚠ " + message;
}
