using System.Text;
using System.Windows.Media;

namespace FolderPurge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly ObservableCollection<string> _targetFolders = [];
    private List<FolderEntry> _scanResults = [];
    private CancellationTokenSource? _cts;
    private bool _isScanning;

    public MainWindow()
    {
        InitializeComponent();
        FolderListBox.ItemsSource = _targetFolders;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
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

    // ── 옵션 ────────────────────────────────────────────────────────────

    private void RecycleBin_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        // 휴지통 선택 시 미리보기 강제 해제 불필요
    }
    private void RecycleBin_Unchecked(object sender, RoutedEventArgs e) { }

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
    };

    private void ShowResults(List<FolderEntry> results)
    {
        ResultListView.ItemsSource = results;

        bool hasItems = results.Count > 0;
        ResultListView.Visibility  = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        EmptyState.Visibility      = hasItems ? Visibility.Collapsed : Visibility.Visible;

        SelectAllBtn.IsEnabled  = hasItems;
        SelectNoneBtn.IsEnabled = hasItems;
        CopyResultBtn.IsEnabled = hasItems;
        DeleteBtn.IsEnabled     = hasItems;

        StatusText.Text = hasItems
            ? $"{results.Count:N0}개 항목 탐지됨  —  삭제할 항목을 선택하세요."
            : "탐지된 항목이 없습니다.";

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
        int successCount = 0;
        int failCount    = 0;

        SetScanningState(true);

        await Task.Run(() =>
        {
            foreach (var item in toDelete)
            {
                if (!Directory.Exists(item.Path) && !File.Exists(item.Path))
                {
                    Dispatcher.Invoke(() =>
                        log.AppendLine($"  [건너뜀] {item.Path}  (이미 삭제됨)"));
                    continue;
                }

                bool ok;
                if (previewOnly)
                {
                    ok = true;
                    Dispatcher.Invoke(() =>
                        log.AppendLine($"  [미리보기] {item.Path}  ({item.SizeText})"));
                }
                else if (useRecycleBin)
                    ok = RecycleBinHelper.MoveToRecycleBin(item.Path);
                else
                    ok = RecycleBinHelper.DeletePermanently(item.Path);

                if (!previewOnly)
                {
                    if (ok)
                    {
                        successCount++;
                        Dispatcher.Invoke(() =>
                            log.AppendLine($"  [삭제] {item.Path}  ({item.SizeText})"));
                    }
                    else
                    {
                        failCount++;
                        Dispatcher.Invoke(() =>
                            log.AppendLine($"  [실패] {item.Path}"));
                    }
                }
                else
                {
                    successCount++;
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
              + $"  |  확보 용량: {FormatSize(toDelete.Where(t => t.IsSelected).Sum(t => t.SizeBytes))}";

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
        ScanBtn.IsEnabled          = !scanning;
        DeleteBtn.IsEnabled        = !scanning && _scanResults.Count > 0;
        SelectAllBtn.IsEnabled     = !scanning && _scanResults.Count > 0;
        SelectNoneBtn.IsEnabled    = !scanning && _scanResults.Count > 0;
        CopyResultBtn.IsEnabled    = !scanning && _scanResults.Count > 0;
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
}
