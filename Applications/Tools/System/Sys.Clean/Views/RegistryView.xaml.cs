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

    private async void BtnFix_Click(object sender, RoutedEventArgs e)
    {
        var toFix = IssueList.SelectedItems.Count > 0
            ? IssueList.SelectedItems.Cast<RegistryIssue>().ToList()
            : _issues;

        if (toFix.Count == 0) return;

        var result = MessageBox.Show(
            $"{toFix.Count}개의 레지스트리 항목을 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
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
            _issues.RemoveAll(i => i.IsFixed);
            IssueList.ItemsSource = null;
            IssueList.ItemsSource = _issues;
            TbStatus.Text = "수정 완료";
            TbCount.Text = _issues.Count > 0 ? $"— 잔여 {_issues.Count}개" : "— 모두 수정됨";

            MessageBox.Show($"레지스트리 수정 완료!\n\n수정된 항목: {toFix.Count(i => i.IsFixed)}개",
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
}
