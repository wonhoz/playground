using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Text;
using System.Windows.Media;

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

    public MainWindow()
    {
        InitializeComponent();
        FolderListBox.ItemsSource   = _targetFolders;
        ExcludeListBox.ItemsSource  = _excludedFolders;
        ArtifactListBox.ItemsSource = _artifactFolderNames;
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
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // 설정 복원
        _settings = AppSettings.Load();
        foreach (var f in _settings.TargetFolders)
            if (Directory.Exists(f)) _targetFolders.Add(f);
        foreach (var ex in _settings.ExcludedFolders)
            _excludedFolders.Add(ex);
        foreach (var a in _settings.VsArtifactFolderNames)
            _artifactFolderNames.Add(a);

        ChkEmpty.IsChecked      = _settings.ScanEmptyFolders;
        ChkVsArtifact.IsChecked = _settings.ScanVsArtifacts;
        ChkEmptyFile.IsChecked  = _settings.ScanEmptyFiles;
        ChkRecycleBin.IsChecked = _settings.UseRecycleBin;
        ChkPreview.IsChecked    = _settings.PreviewOnly;

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
        _settings.SortColumn             = _sortColumn ?? string.Empty;
        _settings.SortDescending         = _sortDirection == ListSortDirection.Descending;
        _settings.Save();
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

    // ── 필터 ────────────────────────────────────────────────────────────

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
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
        ResultListView.ItemsSource = null;
        ResultListView.ItemsSource = list;

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
            });

            _scanResults = await scanner.ScanAsync([.. _targetFolders], prog);
            ShowResults(_scanResults);

            // 스캔 완료 토스트 알림 (창이 비활성 상태일 때)
            if (!IsActive && _scanResults.Count > 0)
            {
                SystemSounds.Asterisk.Play();
                var hwnd = new WindowInteropHelper(this).Handle;
                FlashWindow(hwnd, true);
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
        ScanEmptyFolders   = ChkEmpty.IsChecked == true,
        ScanVsArtifacts    = ChkVsArtifact.IsChecked == true,
        ScanEmptyFiles     = ChkEmptyFile.IsChecked == true,
        UseRecycleBin      = ChkRecycleBin.IsChecked == true,
        PreviewOnly        = ChkPreview.IsChecked == true,
        ExcludedFolderNames = new HashSet<string>(
            _excludedFolders, StringComparer.OrdinalIgnoreCase),
        VsArtifactNames = new HashSet<string>(
            _artifactFolderNames, StringComparer.OrdinalIgnoreCase),
        VsArtifactFileExtensions = new HashSet<string>(
            _settings.VsArtifactFileExtensions, StringComparer.OrdinalIgnoreCase),
    };

    private void ShowResults(List<FolderEntry> results)
    {
        _scanResults = results;

        bool hasItems = results.Count > 0;
        SelectAllBtn.IsEnabled  = hasItems;
        SelectNoneBtn.IsEnabled = hasItems;
        InvertBtn.IsEnabled     = hasItems;
        CopyResultBtn.IsEnabled = hasItems;
        ExportBtn.IsEnabled     = hasItems;
        DeleteBtn.IsEnabled     = hasItems;

        StatusText.Text = hasItems
            ? $"{results.Count:N0}개 항목 탐지됨  —  삭제할 항목을 선택하세요."
            : "탐지된 항목이 없습니다.";

        _settings.LastScanTime = DateTime.Now;
        StatLastScan.Text = _settings.LastScanTime.Value.ToString("MM-dd HH:mm");

        ApplyFilter();
        UpdateStats();
    }

    // ── 전체 선택/해제 ──────────────────────────────────────────────────

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _scanResults) item.IsSelected = true;
        UpdateStats();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _scanResults) item.IsSelected = false;
        UpdateStats();
    }

    private void InvertSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _scanResults) item.IsSelected = !item.IsSelected;
        UpdateStats();
    }

    private void Item_CheckChanged(object sender, RoutedEventArgs e) => UpdateStats();

    private void UpdateStats()
    {
        var selected = _scanResults.Where(r => r.IsSelected).ToList();
        StatFound.Text    = $"{_scanResults.Count:N0}개";
        StatSelected.Text = $"{selected.Count:N0}개";
        StatSize.Text     = FormatSize(selected.Sum(r => r.SizeBytes));
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

    private void Export_Click(object sender, RoutedEventArgs e)
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

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        StatusText.Text = $"결과가 저장되었습니다: {Path.GetFileName(dlg.FileName)}";
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
            string method  = useRecycleBin ? "휴지통으로 이동" : "영구 삭제";
            string warning = useRecycleBin ? "" : "\n\n⚠️ 영구 삭제 선택 시 복구할 수 없습니다.";
            var confirm = System.Windows.MessageBox.Show(
                $"{toDelete.Count:N0}개 항목을 {method}하겠습니까?{warning}",
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

        // 삭제된 항목 목록에서 제거
        if (!previewOnly)
        {
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

        StatusText.Text = summary;
    }

    private void CloseResult_Click(object sender, RoutedEventArgs e)
    {
        DeleteResultPanel.Visibility = Visibility.Collapsed;
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
        if (!scanning) ProgressText.Text = string.Empty;
    }

    // ── 유틸리티 ────────────────────────────────────────────────────────

    private static string FormatSize(long bytes) => bytes switch
    {
        0           => "0 B",
        < 1024      => $"{bytes} B",
        < 1048576   => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _           => $"{bytes / 1073741824.0:F2} GB"
    };

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

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header) return;
        var tag = header.Column?.Header as string;
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

        _scanResults = (_sortColumn, _sortDirection) switch
        {
            ("종류",   ListSortDirection.Ascending)  => [.. _scanResults.OrderBy(r => r.Kind)],
            ("종류",   ListSortDirection.Descending) => [.. _scanResults.OrderByDescending(r => r.Kind)],
            ("경로",   ListSortDirection.Ascending)  => [.. _scanResults.OrderBy(r => r.Path)],
            ("경로",   ListSortDirection.Descending) => [.. _scanResults.OrderByDescending(r => r.Path)],
            ("크기",   ListSortDirection.Ascending)  => [.. _scanResults.OrderBy(r => r.SizeBytes)],
            ("크기",   ListSortDirection.Descending) => [.. _scanResults.OrderByDescending(r => r.SizeBytes)],
            ("항목 수", ListSortDirection.Ascending)  => [.. _scanResults.OrderBy(r => r.ItemCount)],
            ("항목 수", ListSortDirection.Descending) => [.. _scanResults.OrderByDescending(r => r.ItemCount)],
            _ => _scanResults
        };

        ApplyFilter();
    }

    // ── 도움말 ──────────────────────────────────────────────────────────

    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void ShowHelp()
    {
        System.Windows.MessageBox.Show(
            """
            ── 단축키 ──────────────────────────────
            F1          도움말 표시
            Enter       스캔 시작 (폴더 목록에 항목이 있을 때)
            Delete      선택된 항목 삭제 확인

            ── 사용 방법 ────────────────────────────
            1. 폴더 추가
               · [+ 폴더 추가] 버튼 클릭, 또는
               · 탐색기에서 폴더를 드래그 앤 드롭

            2. 스캔 옵션 설정
               · 빈 폴더 / VS 빌드 아티팩트 / 0바이트 파일 선택
               · VS 아티팩트: bin, obj 폴더와 .user 파일만 남은 폴더 탐지

            3. [스캔 시작] 또는 Enter → 결과 목록 확인

            4. 결과 필터링 (필터 바)
               · 종류별 필터: 전체 / 빈 폴더 / VS 아티팩트 / 빈 파일
               · 최소 크기: 지정 KB 이상 항목만 표시
               · 검색: 경로 키워드로 결과 필터링

            5. 삭제할 항목 선택 (체크박스)
               · 전체 선택 / 선택 해제 / 반전 버튼 사용

            6. [삭제] 클릭 또는 Delete 키
               · 휴지통으로 이동 (기본, 복구 가능)
               · 미리보기 모드: 실제 삭제 없이 대상 확인

            ── 팁 ───────────────────────────────────
            · 결과 항목 더블클릭 또는 우클릭 → 탐색기에서 열기
            · 우클릭 → 경로 복사
            · 컬럼 헤더 클릭 → 정렬 (오름/내림차순 토글, 정렬 상태 저장됨)
            · 아티팩트 폴더명 목록에서 bin, obj 외 폴더명 추가 가능
            · 제외 폴더에 추가 시 스캔 대상에서 제외
            """,
            "Folder.Purge 도움말",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
