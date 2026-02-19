using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FileDuplicates.Models;
using FileDuplicates.Services;

namespace FileDuplicates;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string>         _folders = [];
    private readonly ObservableCollection<DuplicateGroup> _groups  = [];

    private CancellationTokenSource? _cts;
    private bool _isScanning;

    public MainWindow()
    {
        InitializeComponent();

        FolderListBox.ItemsSource = _folders;
        GroupsControl.ItemsSource = _groups;

        ThresholdLabel.Text = $"{(int)ThresholdSlider.Value} bit";
        RemoveFolderBtn.IsEnabled = false;
    }

    // ── 폴더 관리 ─────────────────────────────────────────────────────────────

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "스캔할 폴더를 선택하세요." };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        if (!_folders.Contains(dlg.SelectedPath))
            _folders.Add(dlg.SelectedPath);
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderListBox.SelectedItem is string path)
            _folders.Remove(path);
    }

    private void FolderList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RemoveFolderBtn.IsEnabled = FolderListBox.SelectedItem != null;
    }

    // ── 옵션 ──────────────────────────────────────────────────────────────────

    private void Threshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdLabel is null) return;
        ThresholdLabel.Text = $"{(int)e.NewValue} bit";
    }

    private void ImageScan_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdPanel is null) return;
        ThresholdPanel.Visibility = ImageScanCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 스캔 ──────────────────────────────────────────────────────────────────

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning) return;

        if (_folders.Count == 0)
        {
            System.Windows.MessageBox.Show("스캔할 폴더를 하나 이상 추가하세요.",
                "File.Duplicates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _groups.Clear();
        EmptyHint.Visibility  = Visibility.Collapsed;
        SetScanningState(true);

        var options = new ScanOptions
        {
            Folders             = _folders.ToList(),
            IncludeSubfolders   = SubfoldersCheck.IsChecked == true,
            EnableHashScan      = HashScanCheck.IsChecked   == true,
            EnableImageScan     = ImageScanCheck.IsChecked  == true,
            SimilarityThreshold = (int)ThresholdSlider.Value
        };

        _cts = new CancellationTokenSource();

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgress.Value  = p.Total > 0 ? (double)p.Done / p.Total * 100 : 0;
            ProgressText.Text   = $"검사 중: {p.CurrentFile}  ({p.Done}/{p.Total})";
        });

        try
        {
            var results = await FileScanner.ScanAsync(options, progress, _cts.Token);

            foreach (var g in results) _groups.Add(g);

            var totalFiles = results.Sum(g => g.Files.Count);
            var totalSize  = results.Sum(g => g.TotalSize);
            StatusText.Text = results.Count == 0
                ? "중복 파일이 없습니다."
                : $"{results.Count}개 그룹 · {totalFiles}개 파일 · {FormatSize(totalSize)} 절약 가능";

            EmptyHint.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (results.Count == 0) EmptyHint.Text = "중복 파일이 발견되지 않았습니다.";

            SetActionButtons(results.Count > 0);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "스캔이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"스캔 중 오류가 발생했습니다:\n{ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ── 파일 작업 ─────────────────────────────────────────────────────────────

    private void AutoSelect_Click(object sender, RoutedEventArgs e)
    {
        // 각 그룹에서 첫 번째 파일만 유지, 나머지 선택
        foreach (var group in _groups)
        {
            for (int i = 0; i < group.Files.Count; i++)
                group.Files[i].IsSelected = i > 0;
        }
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var group in _groups)
            foreach (var file in group.Files)
                file.IsSelected = false;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _groups.SelectMany(g => g.Files).Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
        {
            System.Windows.MessageBox.Show("삭제할 파일을 선택하세요.",
                "File.Duplicates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            $"선택한 {selected.Count}개 파일을 휴지통으로 보내시겠습니까?",
            "휴지통으로", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        var errors = new List<string>();
        foreach (var f in selected)
        {
            try   { RecycleBinHelper.SendToRecycleBin(f.Path); }
            catch { errors.Add(f.Path); }
        }

        if (errors.Count > 0)
            System.Windows.MessageBox.Show(
                $"다음 파일을 삭제하지 못했습니다:\n{string.Join("\n", errors)}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);

        // 결과 목록 갱신 (삭제된 파일 제거)
        var deletedPaths = selected
            .Where(f => !File.Exists(f.Path))
            .Select(f => f.Path)
            .ToHashSet();

        foreach (var group in _groups.ToList())
        {
            group.Files.RemoveAll(f => deletedPaths.Contains(f.Path));
            if (group.Files.Count < 2) _groups.Remove(group);
        }

        UpdateStatus();
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        var selected = _groups.SelectMany(g => g.Files).Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
        {
            System.Windows.MessageBox.Show("이동할 파일을 선택하세요.",
                "File.Duplicates", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var dlg = new FolderBrowserDialog { Description = "파일을 이동할 폴더를 선택하세요." };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var dest   = dlg.SelectedPath;
        var errors = new List<string>();

        foreach (var f in selected)
        {
            try
            {
                var target = Path.Combine(dest, Path.GetFileName(f.Path));
                // 동일 이름 충돌 처리
                if (File.Exists(target))
                {
                    var name = Path.GetFileNameWithoutExtension(f.Path);
                    var ext  = Path.GetExtension(f.Path);
                    target   = Path.Combine(dest, $"{name}_{DateTime.Now:HHmmss}{ext}");
                }
                File.Move(f.Path, target);
            }
            catch { errors.Add(f.Path); }
        }

        if (errors.Count > 0)
            System.Windows.MessageBox.Show(
                $"다음 파일을 이동하지 못했습니다:\n{string.Join("\n", errors)}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);

        var movedPaths = selected
            .Where(f => !File.Exists(f.Path))
            .Select(f => f.Path)
            .ToHashSet();

        foreach (var group in _groups.ToList())
        {
            group.Files.RemoveAll(f => movedPaths.Contains(f.Path));
            if (group.Files.Count < 2) _groups.Remove(group);
        }

        UpdateStatus();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────────

    private void SetScanningState(bool scanning)
    {
        _isScanning           = scanning;
        ScanBtn.IsEnabled     = !scanning;
        ScanBtn.Content       = scanning ? "스캔 중..." : "스캔 시작";
        ProgressBar.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        ProgressText.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        if (!scanning) ProgressText.Text = "";
        if (scanning)  StatusText.Text   = "스캔 중...";
    }

    private void SetActionButtons(bool enabled)
    {
        AutoSelectBtn.IsEnabled = enabled;
        SelectNoneBtn.IsEnabled = enabled;
        DeleteBtn.IsEnabled     = enabled;
        MoveBtn.IsEnabled       = enabled;
    }

    private void UpdateStatus()
    {
        var totalFiles = _groups.Sum(g => g.Files.Count);
        var totalSize  = _groups.Sum(g => g.TotalSize);
        StatusText.Text = _groups.Count == 0
            ? "모든 중복 파일이 처리되었습니다."
            : $"{_groups.Count}개 그룹 · {totalFiles}개 파일 · {FormatSize(totalSize)} 절약 가능";
        SetActionButtons(_groups.Count > 0);
    }

    private static string FormatSize(long b) => b switch
    {
        < 1024L               => $"{b} B",
        < 1024L * 1024        => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / 1024.0 / 1024:F1} MB",
        _                     => $"{b / 1024.0 / 1024 / 1024:F2} GB"
    };
}
