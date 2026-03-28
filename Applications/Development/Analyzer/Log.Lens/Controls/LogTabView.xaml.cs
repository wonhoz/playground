using System.Linq;
using System.Text;
using System.Windows.Threading;

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

    private DispatcherTimer? _filterTimer;

    // ── 필터 히스토리 ────────────────────────────────────────────────
    private readonly List<string> _filterHistory = new();
    private int _historyIndex = -1;

    // ── 검색 탐색 ─────────────────────────────────────────────────
    private int _navIndex = -1;

    public LogTabView()
    {
        InitializeComponent();
        LstLog.ItemsSource = _visible;

        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer!.Stop();
            RebuildVisible();
        };
    }

    // ── 초기화 ──────────────────────────────────────────────────────
    public void Initialize(string filePath, int maxLines, Encoding? encoding = null)
    {
        _filePath = filePath;
        _maxLines = maxLines;
        TxtFilePath.Text  = filePath;
        TxtLineCount.Text = "로딩 중...";

        _ = Task.Run(() =>
        {
            try
            {
                var svc          = new LogWatcherService(filePath, encoding);
                var initialLines = svc.ReadInitialAndStart(maxLines);

                // ── UI 차단 방지: 로그 파싱을 배경 스레드에서 수행 ──
                var localCounter = 0;
                var parsed = new List<LogLine>(initialLines.Count);
                foreach (var raw in initialLines)
                {
                    if (!string.IsNullOrEmpty(raw))
                        parsed.Add(LogParserService.Parse(++localCounter, raw));
                }

                svc.LinesReceived += OnLinesReceived;

                Dispatcher.Invoke(() =>
                {
                    _watcher      = svc;
                    _lineCounter  = localCounter;

                    // ItemsSource 일시 분리: 대량 Add 시 개별 렌더링 이벤트 억제
                    LstLog.ItemsSource = null;
                    foreach (var line in parsed)
                    {
                        _all.Add(line);
                        if (MatchesFilter(line)) _visible.Add(line);
                    }
                    LstLog.ItemsSource = _visible;

                    UpdateLineCount();
                    AutoScrollIfEnabled();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtLineCount.Text = $"오류: {ex.Message}";
                    EllStatus.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                    TxtWatchStatus.Text = "읽기 실패";
                    TxtWatchStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                });
            }
        });
    }

    /// <summary>MainWindow의 Ctrl+F 단축키 지원</summary>
    public void FocusFilter()
    {
        TxtFilter.Focus();
        TxtFilter.SelectAll();
    }

    /// <summary>MainWindow의 Ctrl+C 단축키 지원 — 선택 줄 클립보드 복사</summary>
    public void CopySelectedLines()
    {
        var items = LstLog.SelectedItems.Cast<LogLine>().ToList();
        if (items.Count == 0) return;
        var text = string.Join(Environment.NewLine, items.Select(l => l.Raw));
        try { Clipboard.SetText(text); } catch { }
    }

    /// <summary>F3 / Shift+F3 검색 결과 탐색</summary>
    public void NavigateMatch(bool forward)
    {
        if (_visible.Count == 0) return;

        if (forward)
            _navIndex = _navIndex < _visible.Count - 1 ? _navIndex + 1 : 0;
        else
            _navIndex = _navIndex > 0 ? _navIndex - 1 : _visible.Count - 1;

        var item = _visible[_navIndex];
        LstLog.SelectedItem = item;
        LstLog.ScrollIntoView(item);
        UpdateNavLabel();
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
        if (rawLines.Count == 0) return;

        // 대량 라인 추가 시 ItemsSource 분리로 개별 렌더링 이벤트 억제
        var detach = rawLines.Count > 200;
        if (detach) LstLog.ItemsSource = null;

        foreach (var raw in rawLines)
        {
            if (string.IsNullOrEmpty(raw)) continue;

            if (_all.Count >= _maxLines && _maxLines > 0)
            {
                var toRemove = Math.Max(1, _maxLines / 10);
                var removed  = _all.GetRange(0, toRemove);
                _all.RemoveRange(0, toRemove);
                foreach (var r in removed)
                    _visible.Remove(r);
            }

            var line = LogParserService.Parse(++_lineCounter, raw);
            _all.Add(line);
            if (MatchesFilter(line))
                _visible.Add(line);
        }

        if (detach) LstLog.ItemsSource = _visible;
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
        UseRegexHL       = ChkRegex.IsChecked == true;
        CaseSensitiveHL  = ChkCase.IsChecked  == true;

        _navIndex = -1;
        UpdateNavLabel();
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

    private void UpdateNavLabel()
    {
        TxtNavIndex.Text = _navIndex >= 0 && _visible.Count > 0
            ? $"{_navIndex + 1}/{_visible.Count}"
            : "";
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
        _historyIndex = -1; // 직접 입력 시 히스토리 위치 초기화
        _filterTimer?.Stop();
        _filterTimer?.Start();
    }

    private void TxtFilter_KeyDown(object sender, KeyEventArgs e)
    {
        if (!IsLoaded) return;

        if (e.Key == Key.Enter)
        {
            // 히스토리 저장 후 즉시 필터 적용
            var text = TxtFilter.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _filterHistory.Remove(text);
                _filterHistory.Insert(0, text);
                if (_filterHistory.Count > 20) _filterHistory.RemoveAt(20);
            }
            _historyIndex = -1;
            _filterTimer?.Stop();
            RebuildVisible();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            // 이전 히스토리
            if (_filterHistory.Count == 0) return;
            _historyIndex = Math.Min(_historyIndex + 1, _filterHistory.Count - 1);
            TxtFilter.Text = _filterHistory[_historyIndex];
            TxtFilter.CaretIndex = TxtFilter.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            // 다음 히스토리 (더 최신 → 빈 문자열)
            if (_historyIndex > 0)
            {
                _historyIndex--;
                TxtFilter.Text = _filterHistory[_historyIndex];
            }
            else
            {
                _historyIndex = -1;
                TxtFilter.Text = "";
            }
            TxtFilter.CaretIndex = TxtFilter.Text.Length;
            e.Handled = true;
        }
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
        _navIndex    = -1;
        UpdateNavLabel();
        UpdateLineCount();
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e) => NavigateMatch(false);
    private void BtnNext_Click(object sender, RoutedEventArgs e) => NavigateMatch(true);

    // ── IDisposable ──────────────────────────────────────────────
    public void Dispose()
    {
        _filterTimer?.Stop();
        _watcher?.Dispose();
    }
}
