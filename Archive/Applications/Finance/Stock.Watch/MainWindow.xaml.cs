using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Stock.Watch.Indicators;
using Stock.Watch.Models;
using Stock.Watch.Services;
using Stock.Watch.Views;

namespace Stock.Watch;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly KisApiClient _api;
    private readonly SlackNotifier _slack;
    private readonly MonitorService _monitor;

    private readonly ObservableCollection<StockVm> _stocks = new();
    private readonly ObservableCollection<AlertLog> _alerts = new();
    private readonly Dictionary<string, IndicatorSet> _cache = new();

    public MainWindow()
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        _config = AppConfig.Load();
        _slack = new SlackNotifier(_config);
        _api = new KisApiClient(_config, () => _config.Save());
        _monitor = new MonitorService(_config, _api, _slack);

        _monitor.StockUpdated += OnStockUpdated;
        _monitor.AlertRaised += OnAlertRaised;
        _monitor.StatusChanged += s => Dispatcher.Invoke(() => StatusText.Text = s);
        _monitor.ErrorOccurred += s => Dispatcher.Invoke(() => FooterText.Text = $"⚠ {s}");

        StockList.ItemsSource = _stocks;
        AlertList.ItemsSource = _alerts;
        foreach (var ws in _config.Watchlist) _stocks.Add(new StockVm(ws));

        StockList.SelectionChanged += OnStockSelected;
        ToggleBtn.Click += OnToggle;
        RefreshBtn.Click += OnRefresh;
        SettingsBtn.Click += OnSettings;
        AddStockBtn.Click += OnAddStock;
        RemoveStockBtn.Click += OnRemoveStock;
        BuyEditor.Changed += OnRulesChanged;
        SellEditor.Changed += OnRulesChanged;

        if (_stocks.Count > 0) StockList.SelectedIndex = 0;
        Closing += (_, _) => { _monitor.Stop(); _monitor.DisposeRealtime(); _config.Save(); _api.Dispose(); _slack.Dispose(); };
    }

    private StockVm? Selected => StockList.SelectedItem as StockVm;

    // ───────────────────────── 종목 선택 ─────────────────────────
    private void OnStockSelected(object? sender, SelectionChangedEventArgs e)
    {
        var vm = Selected;
        if (vm == null) { Chart.Clear(); CondTitle.Text = "조건 설정"; return; }

        CondTitle.Text = $"조건 설정 — {vm.Display}";
        ChartTitle.Text = vm.Display;
        BuyEditor.Bind(vm.Stock.BuyRules, "매수 조건");
        SellEditor.Bind(vm.Stock.SellRules, "매도 조건");

        if (_cache.TryGetValue(vm.Code, out var set))
            RenderChart(vm.Stock, set);
        else
        {
            Chart.Clear();
            _ = FetchForChartAsync(vm.Stock);
        }
    }

    private async Task FetchForChartAsync(WatchedStock stock)
    {
        if (!_config.HasCredentials) return;
        try
        {
            var set = await _monitor.RefreshStockAsync(stock);
            if (set != null) Dispatcher.Invoke(() => { _cache[stock.Code] = set; if (Selected?.Code == stock.Code) RenderChart(stock, set); });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => FooterText.Text = $"⚠ {ex.Message}");
        }
    }

    private void RenderChart(WatchedStock stock, IndicatorSet set)
    {
        Chart.Show(set);
        ChartTitle.Text = stock.Display;
        ChartPrice.Text = stock.LastPrice > 0 ? $"{stock.LastPrice:N0}원" : "";
    }

    // ───────────────────────── 모니터 이벤트 ─────────────────────────
    private void OnStockUpdated(StockUpdate u)
    {
        Dispatcher.Invoke(() =>
        {
            _cache[u.Stock.Code] = u.Indicators;
            var vm = _stocks.FirstOrDefault(s => s.Code == u.Stock.Code);
            vm?.Refresh();
            if (Selected?.Code == u.Stock.Code) RenderChart(u.Stock, u.Indicators);
        });
    }

    private void OnAlertRaised(AlertLog alert)
    {
        Dispatcher.Invoke(() =>
        {
            _alerts.Insert(0, alert);
            while (_alerts.Count > 100) _alerts.RemoveAt(_alerts.Count - 1);
            FooterText.Text = $"🔔 [{alert.KindText}] {alert.Name} {alert.Price:N0}원 — {alert.RuleSummary}";
        });
    }

    // ───────────────────────── 버튼 ─────────────────────────
    private void OnToggle(object sender, RoutedEventArgs e)
    {
        if (_monitor.IsRunning)
        {
            _monitor.Stop();
            ToggleBtn.Content = "▶ 감시 시작";
        }
        else
        {
            if (!_config.HasCredentials)
            {
                MessageBox.Show("먼저 설정에서 KIS APP KEY / APP SECRET을 입력하세요.", "Stock.Watch",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                OnSettings(sender, e);
                return;
            }
            _monitor.Start();
            ToggleBtn.Content = "■ 감시 중지";
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (!_config.HasCredentials) { OnSettings(sender, e); return; }
        var vm = Selected;
        if (vm == null) return;
        RefreshBtn.IsEnabled = false;
        StatusText.Text = "새로고침 중...";
        try { await _monitor.RefreshStockAsync(vm.Stock); }
        catch (Exception ex) { FooterText.Text = $"⚠ {ex.Message}"; }
        finally { RefreshBtn.IsEnabled = true; StatusText.Text = $"갱신 {DateTime.Now:HH:mm:ss}"; }
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_config) { Owner = this };
        if (win.ShowDialog() == true)
            FooterText.Text = "설정이 저장되었습니다.";
    }

    private void OnAddStock(object sender, RoutedEventArgs e)
    {
        string code = new string(NewCodeBox.Text.Trim().Where(char.IsDigit).ToArray());
        if (code.Length != 6) { MessageBox.Show("종목코드 6자리를 입력하세요.", "Stock.Watch"); return; }
        if (_config.Watchlist.Any(s => s.Code == code)) { MessageBox.Show("이미 추가된 종목입니다.", "Stock.Watch"); return; }

        var ws = AppConfig.MakeDefault(code, code);
        _config.Watchlist.Add(ws);
        _stocks.Add(new StockVm(ws));
        _config.Save();
        _monitor.AddRealtimeCode(code);
        NewCodeBox.Clear();
        StockList.SelectedItem = _stocks[^1];
    }

    private void OnRemoveStock(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm == null) return;
        if (MessageBox.Show($"'{vm.Display}'를 관심종목에서 삭제할까요?", "Stock.Watch",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _config.Watchlist.RemoveAll(s => s.Code == vm.Code);
        _stocks.Remove(vm);
        _cache.Remove(vm.Code);
        _monitor.RemoveRealtimeCode(vm.Code);
        _config.Save();
    }

    private void OnRulesChanged()
    {
        // 조건 변경 시 엣지트리거 상태 초기화 후 저장(다음 평가에서 새 조건 기준으로 판정)
        var vm = Selected;
        if (vm != null) { vm.Stock.BuyWasTrue = false; vm.Stock.SellWasTrue = false; }
        _config.Save();
    }
}
