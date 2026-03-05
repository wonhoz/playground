using HashCheck.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace HashCheck.Views;

public class WatchEntryVm : WatchEntry
{
    public string AlgoText => Algorithm switch
    {
        HashAlgorithmKind.MD5    => "MD5",
        HashAlgorithmKind.SHA1   => "SHA-1",
        HashAlgorithmKind.SHA256 => "SHA-256",
        HashAlgorithmKind.SHA512 => "SHA-512",
        _                        => "?"
    };
}

public partial class WatchView : UserControl
{
    private readonly WatchService _watchSvc = new();
    private readonly ObservableCollection<WatchEntryVm> _entries = [];
    private HashAlgorithmKind _algo = HashAlgorithmKind.SHA256;

    public WatchView()
    {
        InitializeComponent();
        LvWatch.ItemsSource = _entries;
        _watchSvc.FileChanged += OnFileChangedNotification;
    }

    // ── 알고리즘 변경 ────────────────────────────────────────────
    private void CbAlgo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _algo = CbAlgo.SelectedIndex switch
        {
            0 => HashAlgorithmKind.MD5,
            1 => HashAlgorithmKind.SHA1,
            2 => HashAlgorithmKind.SHA256,
            3 => HashAlgorithmKind.SHA512,
            _ => HashAlgorithmKind.SHA256
        };
    }

    // ── 폴더 감시 제어 ──────────────────────────────────────────
    private void BtnPickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "감시할 폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        TxtWatchFolder.Text = dlg.FolderName;
        TxtWatchFolder.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        BtnStartWatch.IsEnabled = true;
    }

    private void BtnStartWatch_Click(object sender, RoutedEventArgs e)
    {
        var folder = TxtWatchFolder.Text.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        _watchSvc.StartWatch(folder);
        SetWatchIndicator(true);
        BtnStartWatch.IsEnabled = false;
        BtnStopWatch.IsEnabled  = true;
        (Window.GetWindow(this) as MainWindow)?.SetStatus($"감시 중: {folder}");
    }

    private void BtnStopWatch_Click(object sender, RoutedEventArgs e)
    {
        _watchSvc.StopWatch();
        SetWatchIndicator(false);
        BtnStartWatch.IsEnabled = true;
        BtnStopWatch.IsEnabled  = false;
        (Window.GetWindow(this) as MainWindow)?.SetStatus("감시 중단");
    }

    private void SetWatchIndicator(bool watching)
    {
        if (watching)
        {
            TxtWatchStatus.Text = "● 감시 중";
            TxtWatchStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            WatchIndicator.Background = new SolidColorBrush(Color.FromArgb(30, 0x22, 0xC5, 0x5E));
        }
        else
        {
            TxtWatchStatus.Text = "● 감시 중단";
            TxtWatchStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99));
            WatchIndicator.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
        }
    }

    // ── 파일 추가 ────────────────────────────────────────────────
    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title      = "감시할 파일 선택",
            Filter     = "모든 파일|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;
        _ = AddFilesAsync(dlg.FileNames);
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Files_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            _ = AddFilesAsync(files.Where(File.Exists).ToArray());
    }

    private async Task AddFilesAsync(string[] paths)
    {
        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, true);

        foreach (var path in paths)
        {
            if (_entries.Any(x => x.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;

            try
            {
                await _watchSvc.AddSnapshotAsync(path, _algo);
                // WatchService에서 WatchEntry 가져오기
                var entry = _watchSvc.GetEntries().FirstOrDefault(x => x.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    // WatchEntryVm으로 래핑하여 PropertyChanged 연결
                    var vm = new WatchEntryVm
                    {
                        FilePath    = entry.FilePath,
                        BaseHash    = entry.BaseHash,
                        Algorithm   = entry.Algorithm,
                        CurrentHash = entry.CurrentHash,
                        LastChecked = entry.LastChecked
                    };
                    _entries.Add(vm);
                }
            }
            catch (Exception ex)
            {
                mw?.SetStatus($"오류: {path} — {ex.Message}");
            }
        }

        UpdateSummary();
        BtnVerifyNow.IsEnabled = _entries.Count > 0;
        mw?.ShowProgress(false);
    }

    // ── 즉시 검증 ────────────────────────────────────────────────
    private async void BtnVerifyNow_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0) return;

        BtnVerifyNow.IsEnabled = false;
        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, false);

        var prog = new Progress<int>(v => mw?.ShowProgress(true, false, v, 100));
        var results = await _watchSvc.VerifyAllAsync(prog);

        // ViewModel 업데이트
        foreach (var result in results)
        {
            var vm = _entries.FirstOrDefault(x => x.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase));
            if (vm != null)
            {
                vm.CurrentHash = result.CurrentHash;
                vm.LastChecked = result.LastChecked;
            }
        }

        // 리바인딩 (WatchEntryVm이 INotifyPropertyChanged 상속하지만 AlgoText 등 추가 갱신)
        var copy = _entries.ToList();
        _entries.Clear();
        foreach (var item in copy) _entries.Add(item);

        UpdateSummary();
        BtnVerifyNow.IsEnabled = true;
        mw?.ShowProgress(false);
        mw?.SetStatus($"검증 완료 — {DateTime.Now:HH:mm:ss}");
    }

    // ── 목록 지우기 ──────────────────────────────────────────────
    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _watchSvc.Clear();
        _entries.Clear();
        BtnVerifyNow.IsEnabled = false;
        TxtChangedCount.Text   = "";
        TxtSummary.Text        = "파일을 추가하여 무결성 감시를 시작하세요.";
        (Window.GetWindow(this) as MainWindow)?.SetStatus("목록 초기화");
    }

    // ── 컨텍스트 메뉴 ────────────────────────────────────────────
    private void MenuRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = LvWatch.SelectedItems.Cast<WatchEntryVm>().ToList();
        foreach (var vm in selected)
        {
            _watchSvc.Remove(vm.FilePath);
            _entries.Remove(vm);
        }
        UpdateSummary();
        BtnVerifyNow.IsEnabled = _entries.Count > 0;
    }

    private async void MenuRefreshSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var selected = LvWatch.SelectedItems.Cast<WatchEntryVm>().ToList();
        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, true);

        foreach (var vm in selected)
        {
            try
            {
                await _watchSvc.AddSnapshotAsync(vm.FilePath, vm.Algorithm); // 재스냅샷
                var refreshed = _watchSvc.GetEntries()
                    .FirstOrDefault(x => x.FilePath.Equals(vm.FilePath, StringComparison.OrdinalIgnoreCase));
                if (refreshed != null)
                {
                    vm.CurrentHash = refreshed.CurrentHash;
                    vm.LastChecked = refreshed.LastChecked;
                }
            }
            catch { }
        }

        // 리바인딩
        var copy = _entries.ToList();
        _entries.Clear();
        foreach (var item in copy) _entries.Add(item);

        UpdateSummary();
        mw?.ShowProgress(false);
    }

    // ── 파일 변경 콜백 (WatchService FSW 이벤트) ─────────────────
    private void OnFileChangedNotification(WatchEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            var vm = _entries.FirstOrDefault(x => x.FilePath.Equals(entry.FilePath, StringComparison.OrdinalIgnoreCase));
            if (vm != null)
            {
                vm.CurrentHash = entry.CurrentHash;
                vm.LastChecked = entry.LastChecked;
                // ObservableCollection은 아이템 프로퍼티 변경을 자동 감지하지 않으므로 리바인딩
                var copy = _entries.ToList();
                _entries.Clear();
                foreach (var item in copy) _entries.Add(item);
            }
            UpdateSummary();
            (Window.GetWindow(this) as MainWindow)?.SetStatus($"변경 감지: {entry.FileName}");
        });
    }

    private void UpdateSummary()
    {
        var changed = _entries.Count(x => x.IsChanged);
        TxtChangedCount.Text = changed > 0 ? $"⚠ 변경 {changed}개" : "";
        TxtSummary.Text = _entries.Count == 0
            ? "파일을 추가하여 무결성 감시를 시작하세요."
            : $"감시 중 {_entries.Count}개 파일 | 정상 {_entries.Count - changed}개 | 변경 {changed}개";
    }
}
