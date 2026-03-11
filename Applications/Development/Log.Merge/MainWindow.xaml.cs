using Microsoft.Win32;

namespace LogMerge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ── 상태 ─────────────────────────────────────────────────────────────
    private readonly ObservableCollection<LogSource>  _sources = [];
    private readonly ObservableCollection<LogEntry>   _allEntries = [];    // 전체 (소스별 시간순)
    private readonly ObservableCollection<LogEntry>   _filtered  = [];    // 필터 적용 후

    private readonly List<LogSourceWatcher> _watchers = [];
    private readonly MergeEngine _engine = new();

    private string? _activeCorrelationId;
    private bool    _initialized;

    // 소스 컬러 팔레트
    private static readonly Color[] SourceColors =
    [
        Color.FromRgb(0xFF, 0x9A, 0x3C), // orange
        Color.FromRgb(0x00, 0xC8, 0xFF), // cyan
        Color.FromRgb(0x6E, 0xFF, 0x6E), // green
        Color.FromRgb(0xFF, 0x66, 0xAA), // pink
        Color.FromRgb(0xA8, 0x8B, 0xFF), // purple
        Color.FromRgb(0xFF, 0xD9, 0x3D), // yellow
        Color.FromRgb(0x3A, 0xFF, 0xE6), // teal
        Color.FromRgb(0xFF, 0x6B, 0x6B), // red
    ];

    public MainWindow()
    {
        InitializeComponent();

        // 다크 타이틀바
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        LstSources.ItemsSource = _sources;
        LstEntries.ItemsSource  = _filtered;

        PreviewKeyDown += OnPreviewKeyDown;
        _initialized = true;
    }

    // ── 소스 추가/제거 ────────────────────────────────────────────────────

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "로그 파일 선택",
            Filter      = "로그 파일|*.log;*.txt;*.json;*.xml;*.out;*.err|모든 파일|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
            AddSource(path);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "로그 폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        var files = Directory.GetFiles(dlg.FolderName)
            .Where(IsLogFile)
            .OrderBy(f => f)
            .Take(10);
        foreach (var f in files)
            AddSource(f);
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        _sources.Clear();
        _allEntries.Clear();
        ApplyFilter();
        RefreshHints();
    }

    private void BtnRemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not LogSource src) return;

        var idx = _sources.IndexOf(src);
        if (idx < 0) return;

        if (idx < _watchers.Count)
        {
            _watchers[idx].Dispose();
            _watchers.RemoveAt(idx);
        }
        _sources.Remove(src);

        // 해당 소스의 항목 제거
        for (int i = _allEntries.Count - 1; i >= 0; i--)
            if (_allEntries[i].Source.Id == src.Id)
                _allEntries.RemoveAt(i);

        ApplyFilter();
        RefreshHints();
    }

    private void AddSource(string filePath)
    {
        if (_sources.Any(s => s.FilePath == filePath)) return;

        var src = new LogSource
        {
            FilePath = filePath,
            Label    = Path.GetFileNameWithoutExtension(filePath),
            Color    = SourceColors[_sources.Count % SourceColors.Length],
        };
        _sources.Add(src);
        RefreshHints();

        // 비동기 초기 로드
        var watcher = new LogSourceWatcher(src);
        _watchers.Add(watcher);

        watcher.LinesReceived += OnLinesReceived;

        Task.Run(() =>
        {
            var lines = watcher.ReadInitialAndStart();
            var entries = lines.Select(l => _engine.CreateEntry(src, l)).ToList();

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 타임스탬프 기준 병합 삽입
                foreach (var entry in entries)
                    MergeInsert(entry);
                ApplyFilter();
                UpdateStatus();
            });
        });
    }

    private void OnLinesReceived(LogSource src, IReadOnlyList<string> lines)
    {
        var entries = lines.Select(l => _engine.CreateEntry(src, l)).ToList();

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var entry in entries)
                MergeInsert(entry);
            ApplyFilter();
            UpdateStatus();

            if (ChkAutoScroll.IsChecked == true && LstEntries.Items.Count > 0)
                LstEntries.ScrollIntoView(LstEntries.Items[^1]);
        });
    }

    // ── 병합 삽입 ─────────────────────────────────────────────────────────

    private void MergeInsert(LogEntry entry)
    {
        // 타임스탬프 없으면 마지막에 추가
        if (!entry.Timestamp.HasValue)
        {
            _allEntries.Add(entry);
            return;
        }

        // 이진 탐색으로 삽입 위치 결정
        int lo = 0, hi = _allEntries.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            var midTs = _allEntries[mid].Timestamp;
            if (!midTs.HasValue || midTs.Value <= entry.Timestamp.Value)
                lo = mid + 1;
            else
                hi = mid;
        }
        _allEntries.Insert(lo, entry);
    }

    // ── 필터 ─────────────────────────────────────────────────────────────

    private void LevelFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        ApplyFilter();
    }

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _filtered.Clear();

        var showFatal = ChkFatal.IsChecked == true;
        var showError = ChkError.IsChecked == true;
        var showWarn  = ChkWarn.IsChecked  == true;
        var showInfo  = ChkInfo.IsChecked  == true;
        var showDebug = ChkDebug.IsChecked == true;
        var showNone  = ChkNone.IsChecked  == true;

        Regex? re = null;
        var pattern = TxtFilter.Text.Trim();
        if (!string.IsNullOrEmpty(pattern))
        {
            try { re = new Regex(pattern, RegexOptions.IgnoreCase); }
            catch { }
        }

        foreach (var entry in _allEntries)
        {
            // 레벨 필터
            bool levelOk = entry.Level switch
            {
                LogLevel.Fatal => showFatal,
                LogLevel.Error => showError,
                LogLevel.Warn  => showWarn,
                LogLevel.Info  => showInfo,
                LogLevel.Debug => showDebug,
                _              => showNone,
            };
            if (!levelOk) continue;

            // 정규식 필터
            if (re != null && !re.IsMatch(entry.Raw)) continue;

            // 코릴레이션 ID 하이라이트
            entry.IsHighlighted = _activeCorrelationId != null &&
                entry.CorrelationIds.Contains(_activeCorrelationId);

            _filtered.Add(entry);
        }

        TxtCount.Text  = $"{_filtered.Count:N0} / {_allEntries.Count:N0}";
        UpdateStatus();
    }

    // ── 코릴레이션 ID 추적 ────────────────────────────────────────────────

    private void LstEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstEntries.SelectedItem is not LogEntry entry) return;

        if (entry.CorrelationIds.Count == 0)
        {
            ClearCorrelation();
            return;
        }

        // 첫 번째 ID로 추적
        SetActiveCorrelation(entry.CorrelationIds[0]);
    }

    private void SetActiveCorrelation(string id)
    {
        _activeCorrelationId = id;
        TxtCorrelation.Text  = id.Length > 16 ? id[..16] + "…" : id;
        BdrCorrelation.ToolTip = $"추적 중: {id}\n클릭하여 해제";
        ApplyFilter();
    }

    private void ClearCorrelation()
    {
        _activeCorrelationId = null;
        TxtCorrelation.Text  = "(없음)";
        BdrCorrelation.ToolTip = "클릭하여 추적 해제";
        ApplyFilter();
    }

    private void BdrCorrelation_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ClearCorrelation();
    }

    // ── 드래그 앤 드롭 ────────────────────────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
            if (File.Exists(f)) AddSource(f);
    }

    // ── 단축키 ────────────────────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BtnAddFile_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TxtFilter.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearCorrelation();
            TxtFilter.Clear();
            e.Handled = true;
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────

    private void RefreshHints()
    {
        BdrSourceHint.Visibility = _sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BdrEmptyHint.Visibility  = _allEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        TxtStatus.Text = $"소스: {_sources.Count}개  |  전체: {_allEntries.Count:N0}줄";
        TxtTotal.Text  = $"표시: {_filtered.Count:N0}줄";
        BdrEmptyHint.Visibility = _allEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsLogFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".log" or ".txt" or ".json" or ".xml"
                   or ".out" or ".err" or ".conf" or "" ;
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var w in _watchers) w.Dispose();
        base.OnClosed(e);
    }
}
