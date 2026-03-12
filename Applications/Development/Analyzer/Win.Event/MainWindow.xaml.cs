using Microsoft.Win32;
using System.Windows.Threading;

namespace WinEvent;

public partial class MainWindow : Window
{
    // ── 서비스 ───────────────────────────────────────────────────────
    private readonly EventLogService _logService = new();
    private readonly AlertService    _alertService = new();

    // ── 상태 ─────────────────────────────────────────────────────────
    private List<EventItem>           _allEvents = [];
    private ObservableCollection<EventItem> _filtered = [];
    private bool _isLive;
    private string _currentLogName = "Application";
    private CancellationTokenSource? _loadCts;
    private readonly DispatcherTimer _filterTimer;  // 검색 디바운스

    // DWM 다크 타이틀바
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public MainWindow()
    {
        InitializeComponent();
        _alertService.Load();
        LvEvents.ItemsSource = _filtered;

        // 검색 디바운스 타이머 (300ms)
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); ApplyFilter(); };

        // 실시간 이벤트 핸들러
        _logService.NewEventArrived += OnNewEventArrived;
    }

    // ── 초기화 ───────────────────────────────────────────────────────

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        await LoadEventsAsync("Application");
    }

    // ── 이벤트 로드 ──────────────────────────────────────────────────

    private async Task LoadEventsAsync(string logName)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        SetStatus($"{logName} 로딩 중...");
        BtnRefresh.IsEnabled = false;

        try
        {
            int max = GetMaxCount();
            var events = await _logService.LoadEventsAsync(logName, max, ct);
            if (ct.IsCancellationRequested) return;

            _allEvents = events;
            _currentLogName = logName;
            ApplyFilter();
            SetStatus($"{_filtered.Count:N0}개 표시 / 전체 {_allEvents.Count:N0}개 — {logName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus($"로드 실패: {ex.Message}");
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private async Task LoadFromFileAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        SetStatus($"파일 로딩 중: {System.IO.Path.GetFileName(path)}");
        BtnRefresh.IsEnabled = false;

        try
        {
            var events = await _logService.LoadFromFileAsync(path, 10000, ct);
            if (ct.IsCancellationRequested) return;

            _allEvents = events;
            _currentLogName = path;
            ApplyFilter();
            SetStatus($"{_filtered.Count:N0}개 표시 — {System.IO.Path.GetFileName(path)}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus($"파일 로드 실패: {ex.Message}");
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    // ── 필터 적용 ────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        if (!IsLoaded) return;

        var searchText  = TxtSearch.Text.Trim();
        var eventIdText = TxtEventId.Text.Trim();
        var sourceText  = TxtSource.Text.Trim();

        bool showCritical = ChkCritical.IsChecked == true;
        bool showError    = ChkError.IsChecked    == true;
        bool showWarning  = ChkWarning.IsChecked  == true;
        bool showInfo     = ChkInfo.IsChecked     == true;
        bool showVerbose  = ChkVerbose.IsChecked  == true;

        // EventID 숫자 파싱
        long? filterEventId = null;
        if (long.TryParse(eventIdText, out long parsedId)) filterEventId = parsedId;

        // 정규식 컴파일 (잘못된 정규식은 리터럴 검색으로 폴백)
        Regex? searchRegex = null;
        if (!string.IsNullOrEmpty(searchText))
        {
            try { searchRegex = new Regex(searchText, RegexOptions.IgnoreCase); }
            catch { searchRegex = new Regex(Regex.Escape(searchText), RegexOptions.IgnoreCase); }
        }

        Regex? sourceRegex = null;
        if (!string.IsNullOrEmpty(sourceText))
        {
            try { sourceRegex = new Regex(sourceText, RegexOptions.IgnoreCase); }
            catch { sourceRegex = new Regex(Regex.Escape(sourceText), RegexOptions.IgnoreCase); }
        }

        _filtered.Clear();
        foreach (var item in _allEvents)
        {
            // 레벨 필터
            bool levelOk = item.Level switch
            {
                1 => showCritical,
                2 => showError,
                3 => showWarning,
                4 => showInfo,
                5 => showVerbose,
                _ => showInfo
            };
            if (!levelOk) continue;

            // EventID 필터
            if (filterEventId.HasValue && item.EventId != filterEventId.Value) continue;

            // 소스 필터
            if (sourceRegex is not null && !sourceRegex.IsMatch(item.ProviderName)) continue;

            // 메시지 검색
            if (searchRegex is not null &&
                !searchRegex.IsMatch(item.MessageFull) &&
                !searchRegex.IsMatch(item.ProviderName))
                continue;

            _filtered.Add(item);
        }

        SetStatus($"{_filtered.Count:N0}개 표시 / 전체 {_allEvents.Count:N0}개 — {System.IO.Path.GetFileName(_currentLogName)}");
    }

    // ── 실시간 모드 ──────────────────────────────────────────────────

    private void ToggleLive()
    {
        _isLive = !_isLive;
        if (_isLive)
        {
            _logService.StartWatching(_currentLogName);
            BtnLive.Style = (Style)FindResource("LiveActiveBtn");
            BtnLive.Content = "⏹ 실시간 중지";
            TxtLiveStatus.Text = "● 실시간 수신 중";
            TxtLiveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x40));
        }
        else
        {
            _logService.StopWatching();
            BtnLive.Style = (Style)FindResource("SecondaryBtn");
            BtnLive.Content = "● 실시간";
            TxtLiveStatus.Text = "";
        }
    }

    private void OnNewEventArrived(EventItem item)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _allEvents.Insert(0, item);

            // 필터 조건 통과 시에만 추가
            if (MatchesFilter(item))
            {
                _filtered.Insert(0, item);
                SetStatus($"{_filtered.Count:N0}개 표시 / 전체 {_allEvents.Count:N0}개 — 실시간");
            }

            // 알림 규칙 확인
            var ruleName = _alertService.GetMatchedRuleName(item);
            if (ruleName is not null)
                ShowAlertToast(item, ruleName);

            // 최대 5000개 유지
            if (_allEvents.Count > 5000) _allEvents.RemoveAt(_allEvents.Count - 1);
        });
    }

    private bool MatchesFilter(EventItem item)
    {
        bool showCritical = ChkCritical.IsChecked == true;
        bool showError    = ChkError.IsChecked    == true;
        bool showWarning  = ChkWarning.IsChecked  == true;
        bool showInfo     = ChkInfo.IsChecked     == true;
        bool showVerbose  = ChkVerbose.IsChecked  == true;

        bool levelOk = item.Level switch
        {
            1 => showCritical,
            2 => showError,
            3 => showWarning,
            4 => showInfo,
            5 => showVerbose,
            _ => showInfo
        };
        return levelOk;
    }

    // ── Toast 알림 ───────────────────────────────────────────────────

    private void ShowAlertToast(EventItem item, string ruleName)
    {
        var toast = new Windows.AlertToastWindow(ruleName, item);
        toast.Owner = this;
        toast.Show();
    }

    // ── 상세 패널 ────────────────────────────────────────────────────

    private void ShowDetail(EventItem? item)
    {
        if (item is null) { TxtDetail.Text = ""; return; }
        TxtDetail.Text =
            $"시간:    {item.TimeDisplay}\n" +
            $"레벨:    {item.LevelTag}\n" +
            $"EventID: {item.EventId}\n" +
            $"소스:    {item.ProviderName}\n" +
            $"로그:    {item.LogName}\n" +
            $"PC:      {item.MachineName}\n" +
            $"RecordID:{item.RecordId}\n" +
            $"─────────────────────────────────────────────────────\n" +
            item.MessageFull;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────

    private int GetMaxCount()
    {
        return (CmbMaxCount.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "500"   => 500,
            "2,000" => 2000,
            "5,000" => 5000,
            _       => 1000
        };
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    // ── 이벤트 핸들러: 툴바 ──────────────────────────────────────────

    private async void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_isLive) ToggleLive();
        var src = (CmbSource.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(src) || src.StartsWith("─")) return;
        await LoadEventsAsync(src);
    }

    private async void CmbMaxCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadEventsAsync(_currentLogName);
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(_currentLogName))
            await LoadFromFileAsync(_currentLogName);
        else
            await LoadEventsAsync(_currentLogName);
    }

    private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "이벤트 로그 파일 (*.evtx)|*.evtx|모든 파일|*.*",
            Title  = "EVTX 파일 열기"
        };
        if (dlg.ShowDialog() != true) return;
        if (_isLive) ToggleLive();
        await LoadFromFileAsync(dlg.FileName);
    }

    private void BtnLive_Click(object sender, RoutedEventArgs e) => ToggleLive();

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var csv  = new MenuItem { Header = "CSV로 내보내기...", Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xE0)), Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x28)) };
        var json = new MenuItem { Header = "JSON으로 내보내기...", Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xE0)), Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x28)) };
        csv.Click  += BtnExportCsv_Click;
        json.Click += BtnExportJson_Click;
        menu.Items.Add(csv);
        menu.Items.Add(json);
        menu.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x28));
        menu.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3A));
        menu.PlacementTarget = BtnExport;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void BtnAlertRules_Click(object sender, RoutedEventArgs e)
    {
        var win = new Windows.AlertRulesWindow(_alertService) { Owner = this };
        win.ShowDialog();
    }

    // ── 이벤트 핸들러: 필터 ──────────────────────────────────────────

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    // ── 이벤트 핸들러: 목록 ──────────────────────────────────────────

    private void LvEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowDetail(LvEvents.SelectedItem as EventItem);
    }

    private void LvEvents_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LvEvents.SelectedItem is not EventItem item) return;
        var win = new Windows.EventDetailWindow(item) { Owner = this };
        win.ShowDialog();
    }

    // ── 이벤트 핸들러: 컨텍스트 메뉴 ────────────────────────────────

    private void MenuCopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (LvEvents.SelectedItem is EventItem item)
            Clipboard.SetText(item.MessageFull);
    }

    private void MenuCopyRow_Click(object sender, RoutedEventArgs e)
    {
        if (LvEvents.SelectedItem is EventItem item)
            Clipboard.SetText($"{item.TimeDisplay}\t{item.LevelTag}\t{item.EventId}\t{item.ProviderName}\t{item.MessageShort}");
    }

    private void MenuFilterById_Click(object sender, RoutedEventArgs e)
    {
        if (LvEvents.SelectedItem is EventItem item)
            TxtEventId.Text = item.EventId.ToString();
    }

    private void MenuFilterBySource_Click(object sender, RoutedEventArgs e)
    {
        if (LvEvents.SelectedItem is EventItem item)
            TxtSource.Text = Regex.Escape(item.ProviderName);
    }

    // ── 이벤트 핸들러: 상세 패널 버튼 ───────────────────────────────

    private void BtnCopyDetail_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtDetail.Text))
            Clipboard.SetText(TxtDetail.Text);
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "CSV 파일 (*.csv)|*.csv",
            FileName = $"events_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExportService.ExportCsv(_filtered, dlg.FileName);
            SetStatus($"CSV 내보내기 완료: {System.IO.Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { MessageBox.Show($"내보내기 실패:\n{ex.Message}"); }
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "JSON 파일 (*.json)|*.json",
            FileName = $"events_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExportService.ExportJson(_filtered, dlg.FileName);
            SetStatus($"JSON 내보내기 완료: {System.IO.Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { MessageBox.Show($"내보내기 실패:\n{ex.Message}"); }
    }

    // ── 종료 ────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _logService.Dispose();
        _loadCts?.Cancel();
        base.OnClosed(e);
    }
}
