namespace LogLens.Controls;

public partial class LogTabView : UserControl, IDisposable
{
    // ── DependencyProperties (DataTemplate 내 HighlightTextBlock이 바인딩) ────
    public static readonly DependencyProperty HighlightPatternProperty =
        DependencyProperty.Register(nameof(HighlightPattern), typeof(string), typeof(LogTabView),
            new PropertyMetadata(""));

    public static readonly DependencyProperty UseRegexHLProperty =
        DependencyProperty.Register(nameof(UseRegexHL), typeof(bool), typeof(LogTabView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CaseSensitiveHLProperty =
        DependencyProperty.Register(nameof(CaseSensitiveHL), typeof(bool), typeof(LogTabView),
            new PropertyMetadata(false));

    public string HighlightPattern { get => (string)GetValue(HighlightPatternProperty); private set => SetValue(HighlightPatternProperty, value); }
    public bool   UseRegexHL       { get => (bool)GetValue(UseRegexHLProperty);         private set => SetValue(UseRegexHLProperty, value); }
    public bool   CaseSensitiveHL  { get => (bool)GetValue(CaseSensitiveHLProperty);    private set => SetValue(CaseSensitiveHLProperty, value); }

    // ── 데이터 ──────────────────────────────────────────────────────
    private readonly List<LogLine>                 _all     = new(capacity: 4096);
    private readonly ObservableCollection<LogLine> _visible = new();
    private LogWatcherService? _watcher;
    private string _filePath = "";
    private int    _maxLines = 50_000;
    private int    _lineCounter;

    private System.Windows.Threading.DispatcherTimer? _filterTimer;

    public LogTabView()
    {
        InitializeComponent();
        LstLog.ItemsSource = _visible;

        _filterTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer!.Stop();
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
            try
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
            }
            catch { }
        });
    }

    /// <summary>MainWindow의 Ctrl+F 단축키 지원</summary>
    public void FocusFilter()
    {
        TxtFilter.Focus();
        TxtFilter.SelectAll();
    }

    // ── 새 줄 수신 ──────────────────────────────────────────────────
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

        // 레벨 필터
        var minLevel = GetMinLevel();
        if (minLevel != LogLevel.None && line.Level != LogLevel.None && line.Level < minLevel)
            return false;

        // 텍스트 필터
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
            matches = true; // 잘못된 정규식 → 필터 없이 표시
        }

        return isExclude ? !matches : matches;
    }

    private LogLevel GetMinLevel() => CmbLevel.SelectedIndex switch
    {
        1 => LogLevel.Trace,
        2 => LogLevel.Debug,
        3 => LogLevel.Info,
        4 => LogLevel.Warn,
        5 => LogLevel.Error,
        6 => LogLevel.Fatal,
        _ => LogLevel.None
    };

    // ── visible 전체 재구성 ──────────────────────────────────────
    private void RebuildVisible()
    {
        if (!IsLoaded) return;
        _visible.Clear();
        foreach (var line in _all)
            if (MatchesFilter(line))
                _visible.Add(line);

        // 하이라이트 DP 업데이트 (제외 모드 시 하이라이트 비활성)
        var filter    = TxtFilter.Text.Trim();
        var isExclude = ChkExclude.IsChecked == true;
        HighlightPattern = isExclude ? "" : filter;
        UseRegexHL       = ChkRegex.IsChecked   == true;
        CaseSensitiveHL  = ChkCase.IsChecked    == true;

        UpdateLineCount();
    }

    // ── 상태 업데이트 ────────────────────────────────────────────
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

    private void CmbLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RebuildVisible();
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
