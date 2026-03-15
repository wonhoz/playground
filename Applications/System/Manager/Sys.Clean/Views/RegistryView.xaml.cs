using System.Windows;
using System.Windows.Controls;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class RegistryView : UserControl
{
    private readonly RegistryService _service = new();
    private List<RegistryIssue> _issues = [];
    private CancellationTokenSource? _cts;

    private GridViewColumnHeader? _sortHeader;
    private string? _sortColumn;
    private bool _sortAscending = true;

    public RegistryView()
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

        IssueList.ItemsSource = null;
        _issues.Clear();
        BtnFix.IsEnabled = false;
        BtnScan.Content = "⏹  중지";
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = true;
        TbStatus.Text = "레지스트리 스캔 중...";
        TbCount.Text = "";

        _cts = new CancellationTokenSource();
        try
        {
            _issues = await _service.ScanAsync(_cts.Token);
            IssueList.ItemsSource = _issues;
            ApplySort();
            TbStatus.Text = $"스캔 완료";
            TbCount.Text = _issues.Count > 0
                ? $"— {_issues.Count}개 문제 발견"
                : "— 문제 없음";
            BtnFix.IsEnabled = _issues.Count > 0;
        }
        catch (OperationCanceledException)
        {
            TbStatus.Text = "스캔 취소됨";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            BtnScan.Content = "🔍  레지스트리 스캔";
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
        if (_sortColumn == null || IssueList.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(IssueList.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortColumn,
            _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    private async void BtnFix_Click(object sender, RoutedEventArgs e)
    {
        var toFix = IssueList.SelectedItems.Count > 0
            ? IssueList.SelectedItems.Cast<RegistryIssue>().ToList()
            : _issues;

        if (toFix.Count == 0) return;

        // 백업 먼저 생성
        string backupPath;
        try
        {
            backupPath = BackupService.BackupRegistryIssues(toFix);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"백업 생성 실패: {ex.Message}\n\n안전을 위해 수정을 중단합니다.",
                "백업 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(
            $"{toFix.Count}개의 레지스트리 항목을 수정하시겠습니까?\n\n" +
            $"백업 파일이 생성되었습니다:\n{backupPath}\n\n" +
            "복원하려면 위 파일을 더블클릭하세요.",
            "레지스트리 수정",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        BtnFix.IsEnabled = false;
        BtnScan.IsEnabled = false;
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = true;
        TbStatus.Text = "수정 중...";

        _cts = new CancellationTokenSource();
        try
        {
            await _service.FixIssuesAsync(toFix, _cts.Token);
            int fixedCount = toFix.Count(i => i.IsFixed); // RemoveAll 전에 먼저 집계
            _issues.RemoveAll(i => i.IsFixed);
            IssueList.ItemsSource = null;
            IssueList.ItemsSource = _issues;
            ApplySort();
            TbStatus.Text = "수정 완료";
            TbCount.Text = _issues.Count > 0 ? $"— 잔여 {_issues.Count}개" : "— 모두 수정됨";

            MessageBox.Show(
                $"레지스트리 수정 완료!\n\n" +
                $"수정된 항목: {fixedCount}개\n\n" +
                $"백업 파일: {Path.GetFileName(backupPath)}\n" +
                $"위치: {BackupService.BackupFolder}\n\n" +
                "문제가 발생하면 백업 파일을 더블클릭하여 복원하세요.",
                "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            BtnScan.IsEnabled = true;
            BtnFix.IsEnabled = _issues.Count > 0;
            PbProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        BackupService.OpenBackupFolder();
    }
}
