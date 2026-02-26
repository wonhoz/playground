namespace LogTail.Controls;

public partial class LogTabView : UserControl, IDisposable
{
    // ── 데이터 ──────────────────────────────────────────────────────
    private readonly List<LogLine>                _all     = new(capacity: 4096);
    private readonly ObservableCollection<LogLine> _visible = new();
    private LogWatcherService? _watcher;
    private string _filePath = "";
    private int    _maxLines = 50_000;
    private int    _lineCounter;
    private bool   _showUtc;

    // 필터 갱신 딜레이 (연속 입력 시 과도한 재계산 방지)
    private System.Windows.Threading.DispatcherTimer? _filterTimer;

    public LogTabView()
    {
        InitializeComponent();
        LstLog.ItemsSource = _visible;

        // 필터 딜레이 타이머 (300ms)
        _filterTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            RebuildVisible();
        };
    }

    // ── 초기화 ──────────────────────────────────────────────────────
    public void Initialize(string filePath, int maxLines)
    {
        _filePath = filePath;
        _maxLines = maxLines;
        TxtFilePath.Text = filePath;

        _ = Task.Run(() =>
        {
            var svc          = new LogWatcherService(filePath);
            var initialLines = svc.ReadInitialAndStart(maxLines);
            svc.LinesReceived += OnLinesReceived;

            Dispatcher.Invoke(() =>
            {
                _watcher = svc;
                BulkAdd(initialLines);
                UpdateLineCount();
                AutoScrollIfEnabled();
            });
        });
    }

    // ── 새 줄 수신 (watcher 스레드 → UI 스레드) ──────────────────
    private void OnLinesReceived(IReadOnlyList<string> newLines)
    {
        Dispatcher.Invoke(() =>
        {
            BulkAdd(newLines);
            UpdateLineCount();
            AutoScrollIfEnabled();
        });
    }

    // ── 줄 추가 ──────────────────────────────────────────────────
    private void BulkAdd(IReadOnlyList<string> rawLines)
    {
        foreach (var raw in rawLines)
        {
            if (string.IsNullOrEmpty(raw)) continue;

            // 최대 라인 초과 시 앞부분 10% 제거
            if (_all.Count >= _maxLines && _maxLines > 0)
            {
                var toRemove = Math.Max(1, _maxLines / 10);
                _all.RemoveRange(0, toRemove);
                for (var i = 0; i < toRemove && _visible.Count > 0; i++)
                    _visible.RemoveAt(0);
            }

            var line = LogParserService.Parse(++_lineCounter, raw);
            _all.Add(line);
            if (MatchesFilter(line))
                _visible.Add(line);
        }
    }

    // ── 필터 매칭 ────────────────────────────────────────────────
    private bool MatchesFilter(LogLine line)
    {
        if (!IsLoaded) return true;
        var filter = TxtFilter.Text.Trim();
        if (string.IsNullOrEmpty(filter)) return true;

        var caseSensitive = ChkCase.IsChecked    == true;
        var isRegex       = ChkRegex.IsChecked   == true;
        var isExclude     = ChkExclude.IsChecked == true;

        bool matches;
        try
        {
            if (isRegex)
            {
                var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                matches  = Regex.IsMatch(line.Raw, filter, opts);
            }
            else
            {
                var comparison = caseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;
                matches = line.Raw.Contains(filter, comparison);
            }
        }
        catch
        {
            // 잘못된 정규식 → 필터 없이 표시
            matches = true;
        }

        return isExclude ? !matches : matches;
    }

    // ── visible 컬렉션 전체 재구성 ──────────────────────────────
    private void RebuildVisible()
    {
        if (!IsLoaded) return;
        _visible.Clear();
        foreach (var line in _all)
        {
            if (MatchesFilter(line))
                _visible.Add(line);
        }
        UpdateLineCount();
    }

    // ── 상태 표시 업데이트 ───────────────────────────────────────
    private void UpdateLineCount()
    {
        var shown = _visible.Count;
        var total = _all.Count;
        TxtLineCount.Text = shown == total
            ? $"{total:N0} 줄"
            : $"{shown:N0} / {total:N0} 줄 (필터 적용)";
    }

    private void AutoScrollIfEnabled()
    {
        if (ChkAutoScroll.IsChecked != true || _visible.Count == 0) return;
        LstLog.ScrollIntoView(_visible[^1]);
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────
    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _filterTimer?.Stop();
        _filterTimer?.Start();
    }

    private void FilterOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        RebuildVisible();
    }

    private void BtnTimeMode_Click(object sender, RoutedEventArgs e)
    {
        _showUtc           = !_showUtc;
        BtnTimeMode.Content = _showUtc ? "UTC" : "현지";
        // 타임스탬프 표시 모드 전환 (Raw 표시 유지 — 향후 DisplayText 확장 시 사용)
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _all.Clear();
        _visible.Clear();
        _lineCounter = 0;
        UpdateLineCount();
    }

    // ── IDisposable ──────────────────────────────────────────────
    public void Dispose()
    {
        _filterTimer?.Stop();
        _watcher?.Dispose();
    }
}
