using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;

namespace FolderPurge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

    private readonly ObservableCollection<string> _targetFolders = [];
    private readonly ObservableCollection<string> _excludedFolders = [];
    private readonly ObservableCollection<string> _artifactFolderNames = [];
    private List<FolderEntry> _scanResults = [];
    private CancellationTokenSource? _cts;
    private bool _isScanning;
    private AppSettings _settings = new();
    private string? _sortColumn;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private bool _suppressSelectionSync;
    private IntPtr _hwnd;
    private readonly DispatcherTimer _filterDebounce;

    public MainWindow()
    {
        InitializeComponent();
        FolderListBox.ItemsSource   = _targetFolders;
        ExcludeListBox.ItemsSource  = _excludedFolders;
        ArtifactListBox.ItemsSource = _artifactFolderNames;
        _filterDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterDebounce.Tick += (_, _) => { _filterDebounce.Stop(); ApplyFilter(); };
        Loaded  += OnLoaded;
        Closing += OnClosing;
        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.F1:
                ShowHelp();
                break;
            case System.Windows.Input.Key.Enter:
                if (!_isScanning && _targetFolders.Count > 0)
                    Scan_Click(sender, e);
                break;
            case System.Windows.Input.Key.Delete:
                if (!_isScanning && _scanResults.Any(r => r.IsSelected))
                    Delete_Click(sender, e);
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        _hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(_hwnd, 20, ref dark, sizeof(int));

        // 설정 복원
        _settings = AppSettings.Load();
        foreach (var f in _settings.TargetFolders)
            if (Directory.Exists(f)) _targetFolders.Add(f);
        foreach (var ex in _settings.ExcludedFolders)
            _excludedFolders.Add(ex);
        foreach (var a in _settings.VsArtifactFolderNames)
            _artifactFolderNames.Add(a);

        ChkEmpty.IsChecked         = _settings.ScanEmptyFolders;
        ChkVsArtifact.IsChecked    = _settings.ScanVsArtifacts;
        ChkEmptyFile.IsChecked     = _settings.ScanEmptyFiles;
        ChkRecycleBin.IsChecked    = _settings.UseRecycleBin;
        ChkPreview.IsChecked       = _settings.PreviewOnly;
        ChkAutoRescan.IsChecked    = _settings.AutoScanAfterDelete;
        ChkExcludeRecent.IsChecked = _settings.ExcludeRecentFolders;
        MinAgeDaysInput.Text       = _settings.MinAgeDays.ToString();
        UpdateMinAgeDaysPanel();

        // 정렬 상태 복원
        if (!string.IsNullOrEmpty(_settings.SortColumn))
        {
            _sortColumn    = _settings.SortColumn;
            _sortDirection = _settings.SortDescending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        // 마지막 스캔 시간 표시
        StatLastScan.Text = _settings.LastScanTime.HasValue
            ? _settings.LastScanTime.Value.ToString("MM-dd HH:mm")
            : "—";

        UpdateDropHint();
        if (_settings.PreviewOnly) DeleteBtn.Content = "미리보기 실행";
        UpdateSortIndicator();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.TargetFolders          = [.. _targetFolders];
        _settings.ExcludedFolders        = [.. _excludedFolders];
        _settings.VsArtifactFolderNames  = [.. _artifactFolderNames];
        _settings.ScanEmptyFolders       = ChkEmpty.IsChecked == true;
        _settings.ScanVsArtifacts        = ChkVsArtifact.IsChecked == true;
        _settings.ScanEmptyFiles         = ChkEmptyFile.IsChecked == true;
        _settings.UseRecycleBin          = ChkRecycleBin.IsChecked == true;
        _settings.PreviewOnly            = ChkPreview.IsChecked == true;
        _settings.AutoScanAfterDelete    = ChkAutoRescan.IsChecked == true;
        _settings.ExcludeRecentFolders   = ChkExcludeRecent.IsChecked == true;
        _settings.MinAgeDays             = int.TryParse(MinAgeDaysInput.Text.Trim(), out int ageDays) && ageDays > 0 ? ageDays : 7;
        _settings.SortColumn             = _sortColumn ?? string.Empty;
        _settings.SortDescending         = _sortDirection == ListSortDirection.Descending;
        if (!_settings.Save())
            System.Windows.MessageBox.Show(
                "설정 저장에 실패했습니다.\n다음 실행 시 설정이 유지되지 않을 수 있습니다.",
                "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ── 드래그 앤 드롭 ──────────────────────────────────────────────────

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;

        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x64, 0x3C));
    }

    private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x55));
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x55));

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        foreach (var path in paths)
        {
            if (Directory.Exists(path) && !_targetFolders.Contains(path))
                _targetFolders.Add(path);
        }
        UpdateDropHint();
    }

    // ── 폴더 추가/제거 ──────────────────────────────────────────────────

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "스캔할 폴더를 선택하세요." };
        if (dlg.ShowDialog() != true) return;
        if (!_targetFolders.Contains(dlg.FolderName))
            _targetFolders.Add(dlg.FolderName);
        UpdateDropHint();
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = FolderListBox.SelectedItem as string;
        if (selected != null) _targetFolders.Remove(selected);
        UpdateDropHint();
        RemoveFolderBtn.IsEnabled = _targetFolders.Count > 0;
    }

    private void FolderList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RemoveFolderBtn.IsEnabled = FolderListBox.SelectedItem != null;
    }

    private void UpdateDropHint()
    {
        DropHint.Visibility = _targetFolders.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── 제외 폴더 편집 ──────────────────────────────────────────────────

    private void AddExclude_Click(object sender, RoutedEventArgs e) => AddExcludeEntry();

    private void ExcludeInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) AddExcludeEntry();
    }

    private void AddExcludeEntry()
    {
        var name = ExcludeInput.Text.Trim();
        if (string.IsNullOrEmpty(name) || _excludedFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
            return;
        _excludedFolders.Add(name);
        ExcludeInput.Clear();
    }

    private void RemoveExclude_Click(object sender, RoutedEventArgs e)
    {
        if (ExcludeListBox.SelectedItem is string selected)
            _excludedFolders.Remove(selected);
    }

    // ── VS 아티팩트 폴더명 관리 ──────────────────────────────────────────

    private void AddArtifact_Click(object sender, RoutedEventArgs e) => AddArtifactEntry();

    private void ArtifactInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) AddArtifactEntry();
    }

    private void AddArtifactEntry()
    {
        var name = ArtifactInput.Text.Trim();
        if (string.IsNullOrEmpty(name) || _artifactFolderNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return;
        _artifactFolderNames.Add(name);
        ArtifactInput.Clear();
    }

    private void RemoveArtifact_Click(object sender, RoutedEventArgs e)
    {
        if (ArtifactListBox.SelectedItem is string selected)
            _artifactFolderNames.Remove(selected);
    }

    // ── 옵션 ────────────────────────────────────────────────────────────

    private void Preview_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        DeleteBtn.Content = "미리보기 실행";
    }

    private void Preview_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        DeleteBtn.Content = "삭제";
    }

    private void ExcludeRecent_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateMinAgeDaysPanel();
    }

    private void UpdateMinAgeDaysPanel()
    {
        bool enabled = ChkExcludeRecent.IsChecked == true;
        MinAgeDaysPanel.IsEnabled = enabled;
        MinAgeDaysPanel.Opacity   = enabled ? 1.0 : 0.4;
    }

    // ── 필터 ────────────────────────────────────────────────────────────

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        // RadioButton 변경은 즉시, TextBox 입력은 디바운스
        if (sender is System.Windows.Controls.RadioButton)
            ApplyFilter();
        else
        {
            _filterDebounce.Stop();
            _filterDebounce.Start();
        }
    }

    private FolderKind? ActiveFilterKind()
    {
        if (FilterEmpty.IsChecked == true) return FolderKind.Empty;
        if (FilterVs.IsChecked    == true) return FolderKind.VsArtifact;
        if (FilterFile.IsChecked  == true) return FolderKind.EmptyFile;
        return null; // 전체
    }

    private void ApplyFilter()
    {
        var kind    = ActiveFilterKind();
        var keyword = SearchInput?.Text.Trim() ?? string.Empty;
        long minBytes = 0;
        if (long.TryParse(MinSizeInput?.Text.Trim(), out long minKb) && minKb > 0)
            minBytes = minKb * 1024;

        var filtered = _scanResults.AsEnumerable();
        if (kind != null)
            filtered = filtered.Where(r => r.Kind == kind);
        if (!string.IsNullOrEmpty(keyword))
            filtered = filtered.Where(r => r.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (minBytes > 0)
            filtered = filtered.Where(r => r.SizeBytes >= minBytes);

        var list = filtered.ToList();
        // ItemsSource 재바인딩 시 SelectionChanged가 RemovedItems로 호출되어
        // IsSelected=false 덮어쓰기를 방지 — 가드 플래그로 동기화 일시 중단
        _suppressSelectionSync = true;
        ResultListView.ItemsSource = null;
        ResultListView.ItemsSource = list;
        _suppressSelectionSync = false;

        bool hasItems = list.Count > 0;
        ResultListView.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        EmptyState.Visibility     = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── 스캔 ────────────────────────────────────────────────────────────

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning) return;
        if (_targetFolders.Count == 0)
        {
            System.Windows.MessageBox.Show("스캔할 폴더를 먼저 추가하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isScanning = true;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var opts = BuildOptions();
        var scanner = new FolderScanner(opts, _cts.Token);

        SetScanningState(true);
        DeleteResultPanel.Visibility = Visibility.Collapsed;

        try
        {
            var prog = new Progress<ScanProgress>(p =>
            {
                ProgressText.Text = $"스캔 중... {p.Scanned:N0}개 처리  |  {TrimPath(p.CurrentPath, 60)}";
                if (p.Total > 0)
                {
                    ScanProgressBar.IsIndeterminate = false;
                    ScanProgressBar.Value = p.Percent;
                }
            });

            _scanResults = await scanner.ScanAsync([.. _targetFolders], prog);
            ShowResults(_scanResults);

            // 스캔 완료 토스트 알림 (창이 비활성 상태일 때)
            if (!IsActive && _scanResults.Count > 0)
            {
                SystemSounds.Asterisk.Play();
                FlashWindow(_hwnd, true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "스캔이 취소되었습니다.";
        }
        finally
        {
            SetScanningState(false);
            _isScanning = false;
        }
    }

    private ScanOptions BuildOptions() => new()
    {
        ScanEmptyFolders     = ChkEmpty.IsChecked == true,
        ScanVsArtifacts      = ChkVsArtifact.IsChecked == true,
        ScanEmptyFiles       = ChkEmptyFile.IsChecked == true,
        UseRecycleBin        = ChkRecycleBin.IsChecked == true,
        PreviewOnly          = ChkPreview.IsChecked == true,
        ExcludeRecentFolders = ChkExcludeRecent.IsChecked == true,
        MinAgeDays           = int.TryParse(MinAgeDaysInput.Text.Trim(), out int d) && d > 0 ? d : 7,
        ExcludedFolderNames  = new HashSet<string>(
            _excludedFolders, StringComparer.OrdinalIgnoreCase),
        VsArtifactNames      = new HashSet<string>(
            _artifactFolderNames, StringComparer.OrdinalIgnoreCase),
        VsArtifactFileExtensions = new HashSet<string>(
            _settings.VsArtifactFileExtensions, StringComparer.OrdinalIgnoreCase),
    };

    private List<FolderEntry> ApplySort(List<FolderEntry> list) =>
        (_sortColumn, _sortDirection) switch
        {
            ("종류",   ListSortDirection.Ascending)  => [.. list.OrderBy(r => r.Kind)],
            ("종류",   ListSortDirection.Descending) => [.. list.OrderByDescending(r => r.Kind)],
            ("경로",   ListSortDirection.Ascending)  => [.. list.OrderBy(r => r.Path)],
            ("경로",   ListSortDirection.Descending) => [.. list.OrderByDescending(r => r.Path)],
            ("크기",   ListSortDirection.Ascending)  => [.. list.OrderBy(r => r.SizeBytes)],
            ("크기",   ListSortDirection.Descending) => [.. list.OrderByDescending(r => r.SizeBytes)],
            ("항목 수", ListSortDirection.Ascending)  => [.. list.OrderBy(r => r.ItemCount)],
            ("항목 수", ListSortDirection.Descending) => [.. list.OrderByDescending(r => r.ItemCount)],
            _ => list
        };

    private void ShowResults(List<FolderEntry> results)
    {
        _scanResults = ApplySort(results);

        bool hasItems = results.Count > 0;
        SelectAllBtn.IsEnabled  = hasItems;
        SelectNoneBtn.IsEnabled = hasItems;
        InvertBtn.IsEnabled     = hasItems;
        CopyResultBtn.IsEnabled = hasItems;
        ExportBtn.IsEnabled     = hasItems;
        DeleteBtn.IsEnabled     = hasItems;

        if (hasItems)
        {
            var empty = results.Count(r => r.Kind == FolderKind.Empty);
            var vs    = results.Count(r => r.Kind == FolderKind.VsArtifact);
            var files = results.Count(r => r.Kind == FolderKind.EmptyFile);
            var parts = new List<string>();
            if (empty > 0) parts.Add($"빈 폴더 {empty:N0}");
            if (vs    > 0) parts.Add($"VS 아티팩트 {vs:N0}");
            if (files > 0) parts.Add($"빈 파일 {files:N0}");

            // 루트별 요약 (2개 이상 루트일 때만)
            if (_targetFolders.Count > 1)
            {
                var rootSummary = _targetFolders
                    .Select(root => (Name: Path.GetFileName(root) ?? root,
                                     Count: results.Count(r => r.Path.StartsWith(root, StringComparison.OrdinalIgnoreCase))))
                    .Where(x => x.Count > 0)
                    .Select(x => $"{x.Name}({x.Count:N0})");
                StatusText.Text = string.Join("  /  ", parts)
                    + "  [" + string.Join(", ", rootSummary) + "]"
                    + "  —  삭제할 항목을 선택하세요.";
            }
            else
            {
                StatusText.Text = string.Join("  /  ", parts) + "  —  삭제할 항목을 선택하세요.";
            }
        }
        else
        {
            StatusText.Text = "탐지된 항목이 없습니다.";
        }

        _settings.LastScanTime = DateTime.Now;
        StatLastScan.Text = _settings.LastScanTime.Value.ToString("MM-dd HH:mm");

        ApplyFilter();
        UpdateStats();
    }

    // ── 전체 선택/해제 ──────────────────────────────────────────────────

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var visible = (ResultListView.ItemsSource as IEnumerable<FolderEntry>) ?? _scanResults;
        foreach (var item in visible) item.IsSelected = true;
        UpdateStats();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        var visible = (ResultListView.ItemsSource as IEnumerable<FolderEntry>) ?? _scanResults;
        foreach (var item in visible) item.IsSelected = false;
        UpdateStats();
    }

    private void InvertSelection_Click(object sender, RoutedEventArgs e)
    {
        var visible = (ResultListView.ItemsSource as IEnumerable<FolderEntry>) ?? _scanResults;
        foreach (var item in visible) item.IsSelected = !item.IsSelected;
        UpdateStats();
    }

    private void ResultListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync) return;
        foreach (FolderEntry item in e.AddedItems.OfType<FolderEntry>())
            item.IsSelected = true;
        foreach (FolderEntry item in e.RemovedItems.OfType<FolderEntry>())
            item.IsSelected = false;
        UpdateStats();
    }

    private void Item_CheckChanged(object sender, RoutedEventArgs e) => UpdateStats();

    private void UpdateStats()
    {
        var selected = _scanResults.Where(r => r.IsSelected).ToList();
        StatFound.Text     = $"{_scanResults.Count:N0}개";
        StatSelected.Text  = $"{selected.Count:N0}개";
        StatTotalSize.Text = FormatSize(_scanResults.Sum(r => r.SizeBytes));
        StatSize.Text      = FormatSize(selected.Sum(r => r.SizeBytes));
    }

    // ── 결과 복사 ───────────────────────────────────────────────────────

    private void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResults.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine($"Folder.Purge 스캔 결과 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('─', 60));
        foreach (var item in _scanResults)
            sb.AppendLine($"[{item.KindBadge}] {item.Path}  ({item.SizeText})");
        sb.AppendLine(new string('─', 60));
        sb.AppendLine($"총 {_scanResults.Count:N0}개  /  {FormatSize(_scanResults.Sum(r => r.SizeBytes))}");
        System.Windows.Clipboard.SetText(sb.ToString());
        StatusText.Text = "결과가 클립보드에 복사되었습니다.";
    }

    // ── 내보내기 ─────────────────────────────────────────────────────────

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResults.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "결과 내보내기",
            Filter     = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv",
            FileName   = $"FolderPurge_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".txt"
        };
        if (dlg.ShowDialog() != true) return;

        var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        var sb  = new StringBuilder();

        if (ext == ".csv")
        {
            sb.AppendLine("종류,경로,크기,항목수");
            foreach (var item in _scanResults)
                sb.AppendLine($"\"{item.KindBadge}\",\"{item.Path}\",\"{item.SizeText}\",\"{item.ItemCountText}\"");
        }
        else
        {
            sb.AppendLine($"Folder.Purge 스캔 결과 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('─', 60));
            foreach (var item in _scanResults)
                sb.AppendLine($"[{item.KindBadge}] {item.Path}  ({item.SizeText})");
            sb.AppendLine(new string('─', 60));
            sb.AppendLine($"총 {_scanResults.Count:N0}개  /  {FormatSize(_scanResults.Sum(r => r.SizeBytes))}");
        }

        var fileName = dlg.FileName;
        await File.WriteAllTextAsync(fileName, sb.ToString(), Encoding.UTF8);
        StatusText.Text = $"결과가 저장되었습니다: {Path.GetFileName(fileName)}";
    }

    // ── 삭제 ────────────────────────────────────────────────────────────

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = _scanResults.Where(r => r.IsSelected).ToList();
        if (toDelete.Count == 0)
        {
            System.Windows.MessageBox.Show("삭제할 항목을 선택하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool previewOnly   = ChkPreview.IsChecked == true;
        bool useRecycleBin = ChkRecycleBin.IsChecked == true;

        if (!previewOnly)
        {
            string method    = useRecycleBin ? "휴지통으로 이동" : "영구 삭제";
            string totalSize = FormatSize(toDelete.Sum(r => r.SizeBytes));
            string warning   = useRecycleBin ? "" : "\n\n⚠️ 영구 삭제 선택 시 복구할 수 없습니다.";
            var confirm = System.Windows.MessageBox.Show(
                $"{toDelete.Count:N0}개 항목 ({totalSize})을 {method}하겠습니까?{warning}",
                "삭제 확인", MessageBoxButton.YesNo,
                useRecycleBin ? MessageBoxImage.Question : MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        var log = new StringBuilder();
        int  successCount = 0;
        int  failCount    = 0;
        long freedBytes   = 0;

        SetScanningState(true);

        await Task.Run(() =>
        {
            foreach (var item in toDelete)
            {
                if (!Directory.Exists(item.Path) && !File.Exists(item.Path))
                {
                    log.AppendLine($"  [건너뜀] {item.Path}  (이미 삭제됨)");
                    successCount++;
                    freedBytes += item.SizeBytes;
                    continue;
                }

                if (previewOnly)
                {
                    log.AppendLine($"  [미리보기] {item.Path}  ({item.SizeText})");
                    successCount++;
                    freedBytes += item.SizeBytes;
                }
                else
                {
                    bool ok = useRecycleBin
                        ? RecycleBinHelper.MoveToRecycleBin(item.Path)
                        : RecycleBinHelper.DeletePermanently(item.Path);

                    if (ok)
                    {
                        successCount++;
                        freedBytes += item.SizeBytes;
                        log.AppendLine($"  [삭제] {item.Path}  ({item.SizeText})");
                    }
                    else
                    {
                        failCount++;
                        log.AppendLine($"  [실패] {item.Path}");
                    }
                }
            }
        });

        SetScanningState(false);

        // 삭제된 항목 목록에서 제거 + 이력 기록
        if (!previewOnly)
        {
            if (successCount > 0)
            {
                _settings.AddHistory(new ScanHistoryEntry
                {
                    Time         = DateTime.Now,
                    FoundCount   = _scanResults.Count,
                    FreedBytes   = freedBytes,
                    DeletedCount = successCount,
                    Roots        = [.. _targetFolders]
                });
                _settings.Save();
            }
            foreach (var item in toDelete.Where(t => !Directory.Exists(t.Path) && !File.Exists(t.Path)))
                _scanResults.Remove(item);
            ShowResults(_scanResults);
        }

        // 결과 표시
        string summary = previewOnly
            ? $"미리보기: {successCount:N0}개 항목이 삭제 대상입니다."
            : $"완료: {successCount:N0}개 삭제{(failCount > 0 ? $"  /  {failCount:N0}개 실패" : "")}"
              + $"  |  확보 용량: {FormatSize(freedBytes)}";

        DeleteResultText.Text = summary + "\n" + log.ToString();
        DeleteResultPanel.Visibility = Visibility.Visible;

        // 휴지통 삭제 시 복원 안내 버튼 표시
        OpenRecycleBinBtn.Visibility = !previewOnly && useRecycleBin && successCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        StatusText.Text = summary;

        // 삭제 후 자동 재스캔
        if (!previewOnly && successCount > 0 && ChkAutoRescan.IsChecked == true && _targetFolders.Count > 0)
        {
            await Task.Delay(300); // 삭제 완료 시각적 피드백 후
            Scan_Click(sender, e);
        }
    }

    private void CloseResult_Click(object sender, RoutedEventArgs e)
    {
        DeleteResultPanel.Visibility = Visibility.Collapsed;
    }

    private void OpenRecycleBin_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start("explorer.exe", "shell:RecycleBinFolder"); }
        catch { /* 탐색기 실행 실패 무시 */ }
    }

    // ── 취소 ────────────────────────────────────────────────────────────

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    // ── 상태 전환 ────────────────────────────────────────────────────────

    private void SetScanningState(bool scanning)
    {
        ScanBtn.IsEnabled           = !scanning;
        DeleteBtn.IsEnabled         = !scanning && _scanResults.Count > 0;
        SelectAllBtn.IsEnabled      = !scanning && _scanResults.Count > 0;
        SelectNoneBtn.IsEnabled     = !scanning && _scanResults.Count > 0;
        InvertBtn.IsEnabled         = !scanning && _scanResults.Count > 0;
        CopyResultBtn.IsEnabled     = !scanning && _scanResults.Count > 0;
        ExportBtn.IsEnabled         = !scanning && _scanResults.Count > 0;
        ProgressBarPanel.Visibility = scanning ? Visibility.Visible   : Visibility.Collapsed;
        ProgressText.Visibility     = scanning ? Visibility.Visible   : Visibility.Collapsed;
        if (scanning)
        {
            ScanProgressBar.IsIndeterminate = true;
            ScanProgressBar.Value = 0;
        }
        else
        {
            ProgressText.Text = string.Empty;
        }
    }

    // ── 유틸리티 ────────────────────────────────────────────────────────

    private static string FormatSize(long bytes) => Helpers.SizeFormatter.Format(bytes);

    private static string TrimPath(string path, int maxLen) =>
        path.Length <= maxLen ? path : "..." + path[^(maxLen - 3)..];

    // ── 우클릭 컨텍스트 메뉴 ─────────────────────────────────────────────

    private void ResultListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSelectedInExplorer();
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedInExplorer();
    }

    private void RemoveFromResults_Click(object sender, RoutedEventArgs e)
    {
        var targets = ResultListView.SelectedItems.Cast<FolderEntry>().ToList();
        if (targets.Count == 0) return;
        foreach (var t in targets) _scanResults.Remove(t);
        ApplyFilter();
        UpdateStats();
        StatusText.Text = targets.Count == 1
            ? $"결과에서 제거됨: {Path.GetFileName(targets[0].Path)}"
            : $"결과에서 {targets.Count:N0}개 항목 제거됨";
    }

    private void AddToExclude_Click(object sender, RoutedEventArgs e)
    {
        var entries = ResultListView.SelectedItems.Cast<FolderEntry>().ToList();
        if (entries.Count == 0) return;

        var added = new List<string>();
        foreach (var entry in entries)
        {
            string name = entry.Kind == FolderKind.EmptyFile
                ? Path.GetFileName(Path.GetDirectoryName(entry.Path) ?? entry.Path)
                : Path.GetFileName(entry.Path);
            if (string.IsNullOrEmpty(name)) continue;
            if (_excludedFolders.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            _excludedFolders.Add(name);
            added.Add(name);
        }

        StatusText.Text = added.Count switch
        {
            0 => "선택한 항목이 이미 모두 제외 목록에 있습니다.",
            1 => $"제외 폴더에 추가됨: {added[0]}  (다음 스캔부터 적용)",
            _ => $"제외 폴더에 {added.Count}개 추가됨  (다음 스캔부터 적용)"
        };
    }

    private void OpenSelectedInExplorer()
    {
        if (ResultListView.SelectedItem is not FolderEntry entry) return;
        var target = Directory.Exists(entry.Path)
            ? entry.Path
            : Path.GetDirectoryName(entry.Path) ?? entry.Path;
        try { Process.Start("explorer.exe", $"\"{target}\""); }
        catch { /* 탐색기 실행 실패 무시 */ }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ResultListView.SelectedItem is not FolderEntry entry) return;
        System.Windows.Clipboard.SetText(entry.Path);
        StatusText.Text = "경로가 클립보드에 복사되었습니다.";
    }

    // ── 컬럼 클릭 정렬 ──────────────────────────────────────────────────

    private void UpdateSortIndicator()
    {
        if (ResultListView.View is not GridView gv) return;
        foreach (var col in gv.Columns)
        {
            if (col.Header is string h)
                col.Header = h.TrimEnd().TrimEnd('▲', '▼').TrimEnd();
        }
        if (string.IsNullOrEmpty(_sortColumn)) return;
        var target = gv.Columns.FirstOrDefault(c =>
            (c.Header as string)?.Trim() == _sortColumn);
        if (target != null)
            target.Header = _sortColumn + (_sortDirection == ListSortDirection.Ascending ? " ▲" : " ▼");
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header) return;
        var tag = (header.Column?.Header as string)?.TrimEnd().TrimEnd('▲', '▼').TrimEnd();
        if (string.IsNullOrEmpty(tag)) return;

        if (_sortColumn == tag)
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
        {
            _sortColumn    = tag;
            _sortDirection = ListSortDirection.Ascending;
        }

        _scanResults = ApplySort(_scanResults);

        ApplyFilter();
        UpdateSortIndicator();
    }

    // ── 도움말 ──────────────────────────────────────────────────────────

    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void ShowHelp()
    {
        var win = new HelpWindow(_settings.ScanHistory) { Owner = this };
        win.ShowDialog();
    }
}
