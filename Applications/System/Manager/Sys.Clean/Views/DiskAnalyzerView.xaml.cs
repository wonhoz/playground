using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class DiskAnalyzerView : UserControl
{
    private readonly LargeFileService _service = new();
    private List<LargeFileEntry> _files = [];
    private CancellationTokenSource? _cts;

    private GridViewColumnHeader? _sortHeader;
    private string? _sortColumn;
    private bool _sortAscending = true;

    private static readonly long[] MinSizeValues =
    [
        10L * 1024 * 1024,      // 10 MB
        50L * 1024 * 1024,      // 50 MB
        100L * 1024 * 1024,     // 100 MB
        500L * 1024 * 1024,     // 500 MB
        1024L * 1024 * 1024,    // 1 GB
    ];

    public DiskAnalyzerView()
    {
        InitializeComponent();
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            return;
        }

        var path = TxtPath.Text.Trim();
        if (!Directory.Exists(path))
        {
            DarkMessageBox.Show("유효한 폴더 경로를 입력하세요.", "경로 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var minSize = MinSizeValues[CbMinSize.SelectedIndex];

        FileList.ItemsSource = null;
        _files.Clear();
        BtnScan.Content = "⏹  중지";
        BtnDelete.IsEnabled = false;
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = true;

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(dir =>
            TbStatus.Text = $"스캔 중: {dir}");

        try
        {
            _files = await _service.ScanAsync(path, minSize, progress, _cts.Token);
            FileList.ItemsSource = _files;
            ApplySort();

            var totalSize = _files.Sum(f => f.SizeBytes);
            TbStatus.Text = $"스캔 완료 — {_files.Count}개 파일 ({CleanTarget.FormatSize(totalSize)})";
            BtnDelete.IsEnabled = _files.Count > 0;
        }
        catch (OperationCanceledException)
        {
            TbStatus.Text = "스캔 취소됨";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            BtnScan.Content = "🔍  스캔";
            PbProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Tag is not string column)
            return;

        if (_sortHeader != null && _sortHeader != header)
            _sortHeader.Content = ((string)_sortHeader.Content).TrimEnd(' ', '▲', '▼');

        if (_sortColumn == column) _sortAscending = !_sortAscending;
        else { _sortColumn = column; _sortAscending = true; }

        header.Content = ((string)header.Content).TrimEnd(' ', '▲', '▼') + (_sortAscending ? " ▲" : " ▼");
        _sortHeader = header;
        ApplySort();
    }

    private void ApplySort()
    {
        if (_sortColumn == null || FileList.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(FileList.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortColumn,
            _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is LargeFileEntry entry)
        {
            if (File.Exists(entry.FullPath))
                Process.Start("explorer.exe", $"/select,\"{entry.FullPath}\"");
            else if (Directory.Exists(entry.Directory))
                Process.Start("explorer.exe", entry.Directory);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<LargeFileEntry>().ToList();
        if (selected.Count == 0)
        {
            DarkMessageBox.Show("삭제할 파일을 선택하세요.", "삭제",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalSize = selected.Sum(f => f.SizeBytes);
        var result = DarkMessageBox.Show(
            $"선택한 {selected.Count}개 파일 ({CleanTarget.FormatSize(totalSize)})을 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "파일 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        int deleted = 0;
        long freed = 0;
        foreach (var file in selected)
        {
            try
            {
                if (File.Exists(file.FullPath))
                {
                    File.Delete(file.FullPath);
                    _files.Remove(file);
                    freed += file.SizeBytes;
                    deleted++;
                }
            }
            catch { /* access denied */ }
        }

        FileList.ItemsSource = null;
        FileList.ItemsSource = _files;
        ApplySort();

        TbStatus.Text = deleted > 0
            ? $"{deleted}개 파일 삭제 — {CleanTarget.FormatSize(freed)} 해제"
            : "삭제할 수 없는 파일입니다.";

        (Application.Current.MainWindow as MainWindow)?.UpdateDiskInfo();
    }
}
