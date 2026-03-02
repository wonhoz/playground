namespace FileUnlocker;

public partial class MainWindow : Window
{
    // 다크 타이틀바
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly ObservableCollection<TargetFile> _targets = [];
    private readonly ObservableCollection<LockingProcess> _processes = [];

    public MainWindow()
    {
        InitializeComponent();
        FileListBox.ItemsSource  = _targets;
        ProcessListView.ItemsSource = _processes;

        // 우클릭 컨텍스트 메뉴 다크 테마 적용
        if (ProcessListView.ContextMenu is ContextMenu cm)
        {
            cm.Background  = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            cm.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            cm.Foreground  = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            foreach (var item in cm.Items)
            {
                if (item is MenuItem mi)
                {
                    mi.Background = Brushes.Transparent;
                    mi.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                }
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    // ── 드래그 앤 드롭 ──────────────────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            DropZone.Background = (Brush)FindResource("DropHlBrush");
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropZone.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x28));

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

        AddTargets(paths);
    }

    private void AddTargets(IEnumerable<string> paths)
    {
        bool added = false;
        foreach (var p in paths)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) continue;
            if (_targets.Any(t => t.Path.Equals(p, StringComparison.OrdinalIgnoreCase))) continue;
            _targets.Add(new TargetFile { Path = p });
            added = true;
        }

        if (!added) return;

        UpdateDropHint();
        _ = ScanAsync();
    }

    private void UpdateDropHint()
    {
        DropHint.Visibility    = _targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RemoveFileBtn.IsEnabled = _targets.Count > 0;
        StatFiles.Text = $"{_targets.Count}개";
    }

    // ── 스캔 ─────────────────────────────────────────────────────────

    private CancellationTokenSource? _cts;

    private async Task ScanAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SetBusy(true, "검사 중...");

        try
        {
            var targetPaths = _targets.Select(t => t.Path).ToList();

            // 비동기로 프로세스 검색
            var found = await Task.Run(() =>
            {
                var expandedPaths = RestartManagerService.ExpandPaths(targetPaths).ToList();
                return RestartManagerService.GetLockingProcesses(expandedPaths);
            }, token);

            if (token.IsCancellationRequested) return;

            // 대상별 잠금 개수 업데이트
            UpdatePerFileLockCount(found);

            // 프로세스 목록 갱신
            _processes.Clear();
            foreach (var p in found)
                _processes.Add(p);

            UpdateUi();
        }
        catch (OperationCanceledException) { }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdatePerFileLockCount(List<LockingProcess> allProcs)
    {
        // 각 대상 파일이 잠겨 있는지 여부를 개별 검사
        foreach (var target in _targets)
        {
            var expanded = RestartManagerService.ExpandPaths([target.Path]).ToList();
            var lockCount = allProcs.Count > 0
                ? RestartManagerService.GetLockingProcesses(expanded).Count
                : 0;
            target.LockCount = lockCount;
        }
    }

    private void UpdateUi()
    {
        int procCount = _processes.Count;
        int selCount  = _processes.Count(p => p.IsSelected);

        StatProcs.Text    = $"{procCount}개";
        StatSelected.Text = $"{selCount}개";

        bool hasProcs = procCount > 0;
        bool hasSel   = selCount > 0;

        EmptyState.Visibility    = _targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoLockState.Visibility   = _targets.Count > 0 && !hasProcs ? Visibility.Visible : Visibility.Collapsed;
        ProcessListView.Visibility = hasProcs ? Visibility.Visible : Visibility.Collapsed;

        SelectAllBtn.IsEnabled    = hasProcs;
        SelectNoneBtn.IsEnabled   = hasProcs;
        RefreshBtn.IsEnabled      = _targets.Count > 0;
        KillSelectedBtn.IsEnabled = hasSel;
        KillAllBtn.IsEnabled      = hasProcs;

        StatusText.Text = _targets.Count == 0
            ? "파일·폴더를 왼쪽 영역이나 창에 드래그 앤 드롭하세요."
            : hasProcs
                ? $"{procCount}개 프로세스가 파일을 잠금 중입니다."
                : "잠금 중인 프로세스가 없습니다.";
    }

    private void SetBusy(bool busy, string label = "")
    {
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ProgressLabel.Text = label;
        KillSelectedBtn.IsEnabled = !busy;
        KillAllBtn.IsEnabled      = !busy;
        RefreshBtn.IsEnabled      = !busy;
    }

    // ── 프로세스 종료 ────────────────────────────────────────────────

    private async void KillSelected_Click(object sender, RoutedEventArgs e)
        => await KillProcessesAsync(_processes.Where(p => p.IsSelected).ToList());

    private async void KillAll_Click(object sender, RoutedEventArgs e)
        => await KillProcessesAsync(_processes.ToList());

    private async Task KillProcessesAsync(List<LockingProcess> targets)
    {
        if (targets.Count == 0) return;

        // 시스템 프로세스 경고
        var critical = targets.Where(p => p.AppType == "시스템").ToList();
        if (critical.Count > 0)
        {
            var names = string.Join(", ", critical.Select(p => p.Name));
            var res = MessageBox.Show(
                $"다음 프로세스는 시스템 프로세스입니다:\n{names}\n\n강제 종료하면 시스템이 불안정해질 수 있습니다.\n계속하시겠습니까?",
                "경고 — 시스템 프로세스",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
        }

        SetBusy(true, "프로세스 종료 중...");

        var killed  = new List<string>();
        var failed  = new List<string>();

        await Task.Run(() =>
        {
            foreach (var lp in targets)
            {
                try
                {
                    var proc = Process.GetProcessById(lp.Pid);
                    proc.Kill(entireProcessTree: false);
                    proc.WaitForExit(3000);
                    killed.Add(lp.Name);
                }
                catch (Exception ex)
                {
                    failed.Add($"{lp.Name} ({ex.Message})");
                }
            }
        });

        SetBusy(false);
        ShowResult(killed, failed);

        // 종료 후 자동 새로고침
        await Task.Delay(500);
        await ScanAsync();
    }

    private void ShowResult(List<string> killed, List<string> failed)
    {
        if (killed.Count == 0 && failed.Count == 0) return;

        string msg = "";
        if (killed.Count > 0)
            msg += $"종료 완료: {string.Join(", ", killed)}";
        if (failed.Count > 0)
            msg += (msg.Length > 0 ? "\n" : "") + $"실패: {string.Join(", ", failed)}";

        ResultIcon.Text      = failed.Count == 0 ? "✔" : "⚠";
        ResultIcon.Foreground = failed.Count == 0
            ? (Brush)FindResource("GreenBrush")
            : (Brush)FindResource("WarnBrush");
        ResultText.Text      = msg;
        ResultText.Foreground = failed.Count == 0
            ? new SolidColorBrush(Color.FromRgb(0xA0, 0xD0, 0xA0))
            : new SolidColorBrush(Color.FromRgb(0xD0, 0xB0, 0x60));
        ResultPanel.Background = failed.Count == 0
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x1A))
            : new SolidColorBrush(Color.FromRgb(0x2A, 0x1E, 0x0A));
        ResultPanel.BorderBrush = failed.Count == 0
            ? new SolidColorBrush(Color.FromRgb(0x2A, 0x4A, 0x2A))
            : new SolidColorBrush(Color.FromRgb(0x4A, 0x3A, 0x0A));
        ResultPanel.Visibility = Visibility.Visible;
    }

    private void CloseResult_Click(object sender, RoutedEventArgs e)
        => ResultPanel.Visibility = Visibility.Collapsed;

    // ── 선택 ─────────────────────────────────────────────────────────

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _processes) p.IsSelected = true;
        UpdateUi();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _processes) p.IsSelected = false;
        UpdateUi();
    }

    private void Process_CheckChanged(object sender, RoutedEventArgs e) => UpdateUi();

    // ── 파일 목록 관리 ───────────────────────────────────────────────

    private void FileList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RemoveFileBtn.IsEnabled = FileListBox.SelectedItem != null;
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        var sel = FileListBox.SelectedItems.Cast<TargetFile>().ToList();
        foreach (var t in sel) _targets.Remove(t);
        UpdateDropHint();
        if (_targets.Count > 0)
            _ = ScanAsync();
        else
            ClearProcesses();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _targets.Clear();
        UpdateDropHint();
        ClearProcesses();
    }

    private void ClearProcesses()
    {
        _processes.Clear();
        ResultPanel.Visibility = Visibility.Collapsed;
        UpdateUi();
    }

    // ── 새로고침 ─────────────────────────────────────────────────────

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_targets.Count == 0) return;
        await ScanAsync();
    }

    // ── 컨텍스트 메뉴 ────────────────────────────────────────────────

    private LockingProcess? GetContextProc()
        => ProcessListView.SelectedItem as LockingProcess;

    private async void CtxKill_Click(object sender, RoutedEventArgs e)
    {
        var p = GetContextProc();
        if (p == null) return;
        await KillProcessesAsync([p]);
    }

    private void CtxOpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        var p = GetContextProc();
        if (p == null) return;

        if (!string.IsNullOrEmpty(p.ExecutablePath) && File.Exists(p.ExecutablePath))
            Process.Start("explorer.exe", $"/select,\"{p.ExecutablePath}\"");
        else
            Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    }

    private void CtxCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var p = GetContextProc();
        if (p == null || string.IsNullOrEmpty(p.ExecutablePath)) return;
        Clipboard.SetText(p.ExecutablePath);
    }

    private void CtxCopyName_Click(object sender, RoutedEventArgs e)
    {
        var p = GetContextProc();
        if (p == null) return;
        Clipboard.SetText(p.Name);
    }
}
