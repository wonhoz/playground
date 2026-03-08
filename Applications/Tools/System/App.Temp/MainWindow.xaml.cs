using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AppTemp;

public partial class MainWindow : Window
{
    private readonly SandboxService _svc = new();
    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _exePath;

    public MainWindow()
    {
        InitializeComponent();

        _svc.RecordAdded  += OnRecordAdded;
        _svc.StateChanged += OnStateChanged;
        _svc.ProcessExited += OnProcessExited;
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();

        SetupFilteredViews();
    }

    private void SetupFilteredViews()
    {
        var allView = System.Windows.Data.CollectionViewSource.GetDefaultView(_svc.Records);

        // FileList 필터
        var fileView = new System.Windows.Data.ListCollectionView(_svc.Records);
        fileView.Filter = o => o is ChangeRecord r && r.Category == ChangeCategory.File;
        FileList.ItemsSource = fileView;

        // RegList 필터
        var regView = new System.Windows.Data.ListCollectionView(_svc.Records);
        regView.Filter = o => o is ChangeRecord r && r.Category == ChangeCategory.Registry;
        RegList.ItemsSource = regView;
    }

    // ── EXE 선택 ──────────────────────────────────────────────────
    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "실행 파일 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            SetExePath(dlg.FileName);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetExePath(files[0]);
    }

    private void SetExePath(string path)
    {
        _exePath = path;
        TxtExePath.Text       = path;
        TxtExePath.Foreground = (Brush)FindResource("FgBrush");
        BtnLaunch.IsEnabled   = _svc.State == SandboxState.Idle || _svc.State == SandboxState.Stopped;
    }

    // ── 실행/중지 ─────────────────────────────────────────────────
    private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_exePath == null) return;
        BtnLaunch.IsEnabled = false;
        BtnStop.IsEnabled   = true;

        try
        {
            await _svc.StartAsync(_exePath);
            _elapsedTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"실행 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnLaunch.IsEnabled = true;
            BtnStop.IsEnabled   = false;
        }
    }

    private async void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        BtnStop.IsEnabled = false;
        await _svc.StopAsync();
        _elapsedTimer.Stop();
    }

    // ── 롤백/유지/리포트 ──────────────────────────────────────────
    private async void RollbackBtn_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(
            "생성된 파일을 삭제하고 레지스트리 변경을 복원합니다.\n계속하시겠습니까?",
            "롤백 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        BtnRollback.IsEnabled = false;
        BtnKeep.IsEnabled     = false;

        var (files, regs, errors) = await _svc.RollbackAsync();

        var msg = $"롤백 완료\n파일 {files}건 삭제, 레지스트리 {regs}건 복원";
        if (errors.Count > 0) msg += $"\n\n오류 {errors.Count}건:\n" + string.Join("\n", errors.Take(5));
        MessageBox.Show(msg, "롤백 결과", MessageBoxButton.OK, MessageBoxImage.Information);

        TxtActionHint.Text = $"롤백 완료 — 파일 {files}건 삭제, 레지스트리 {regs}건 복원";
    }

    private void KeepBtn_Click(object sender, RoutedEventArgs e)
    {
        BtnRollback.IsEnabled = false;
        BtnKeep.IsEnabled     = false;
        TxtActionHint.Text    = "변경 사항이 유지되었습니다.";
    }

    private void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "리포트 저장",
            Filter = "HTML 리포트 (*.html)|*.html|CSV 파일 (*.csv)|*.csv",
            FileName = $"AppTemp_Report_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FilterIndex == 1 || dlg.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                _svc.ExportHtml(dlg.FileName);
            else
                _svc.ExportCsv(dlg.FileName);

            MessageBox.Show($"리포트 저장 완료:\n{dlg.FileName}", "저장 완료",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 이벤트 처리 ──────────────────────────────────────────────
    private void OnRecordAdded(ChangeRecord rec)
    {
        Dispatcher.Invoke(() =>
        {
            TxtFileCount.Text = _svc.FileChanges.ToString();
            TxtRegCount.Text  = _svc.RegChanges.ToString();
            TxtFileTab.Text   = $" ({_svc.FileChanges})";
            TxtRegTab.Text    = $" ({_svc.RegChanges})";

            // 마지막 항목으로 스크롤
            if (rec.Category == ChangeCategory.File && FileList.Items.Count > 0)
                FileList.ScrollIntoView(FileList.Items[FileList.Items.Count - 1]);
        });
    }

    private void OnStateChanged(SandboxState state)
    {
        Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case SandboxState.Running:
                    TxtStatus.Text      = "실행 중";
                    StatusDot.Fill      = (Brush)FindResource("GreenBrush");
                    BtnStop.IsEnabled   = true;
                    BtnLaunch.IsEnabled = false;
                    BtnRollback.IsEnabled = false;
                    BtnKeep.IsEnabled     = false;
                    BtnExport.IsEnabled   = false;
                    TxtActionHint.Text  = "프로세스 실행 중 — 파일시스템과 레지스트리 변경을 추적하고 있습니다.";
                    break;

                case SandboxState.Stopped:
                    TxtStatus.Text      = "종료됨";
                    StatusDot.Fill      = (Brush)FindResource("OrangeBrush");
                    BtnStop.IsEnabled   = false;
                    BtnLaunch.IsEnabled = _exePath != null;
                    BtnRollback.IsEnabled = true;
                    BtnKeep.IsEnabled     = true;
                    BtnExport.IsEnabled   = true;
                    TxtActionHint.Text  = "실행 완료. 변경 내용을 확인하고 롤백 또는 유지를 선택하세요.";
                    _elapsedTimer.Stop();
                    break;
            }
        });
    }

    private void OnProcessExited()
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text  = "프로세스 종료 (레지스트리 분석 중...)";
            StatusDot.Fill  = (Brush)FindResource("OrangeBrush");
        });
    }

    private void UpdateElapsed()
    {
        if (_svc.StartTime == null) return;
        var elapsed = DateTime.Now - _svc.StartTime.Value;
        TxtElapsed.Text = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s"
            : $"{elapsed.Minutes}m {elapsed.Seconds}s";
    }

    // ── 윈도우 이벤트 ────────────────────────────────────────────
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _svc.Dispose();
        base.OnClosed(e);
    }
}
