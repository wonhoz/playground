using System.Windows;
using System.Windows.Controls;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class StartupView : UserControl
{
    private readonly StartupService _service = new();
    private List<StartupEntry> _allEntries = [];

    public StartupView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        _allEntries = _service.GetEntries();
        ApplyFilter();
        TbStatus.Text = $"총 {_allEntries.Count}개 시작 프로그램";
    }

    private void ApplyFilter()
    {
        if (!IsLoaded) return;
        var search = TxtSearch.Text.Trim().ToLower();
        StartupList.ItemsSource = string.IsNullOrEmpty(search)
            ? _allEntries
            : _allEntries.Where(e => e.Name.ToLower().Contains(search) ||
                                     e.Command.ToLower().Contains(search)).ToList();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void StartupList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (StartupList.SelectedItem is not StartupEntry entry) return;
        ToggleEntry(entry);
    }

    private void BtnEnable_Click(object sender, RoutedEventArgs e)
    {
        foreach (StartupEntry entry in StartupList.SelectedItems)
            if (!entry.IsEnabled) ToggleEntry(entry);
    }

    private void BtnDisable_Click(object sender, RoutedEventArgs e)
    {
        foreach (StartupEntry entry in StartupList.SelectedItems)
            if (entry.IsEnabled) ToggleEntry(entry);
    }

    private void ToggleEntry(StartupEntry entry)
    {
        bool newState = !entry.IsEnabled;
        if (_service.SetEnabled(entry, newState))
        {
            // IsEnabled는 서비스 내에서 업데이트됨 (바인딩 갱신을 위해 리스트 재할당)
            StartupList.Items.Refresh();
            TbStatus.Text = $"'{entry.Name}' — {(newState ? "활성화" : "비활성화")} 완료";
        }
        else
        {
            MessageBox.Show("관리자 권한이 필요합니다.", "권한 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = StartupList.SelectedItems.Cast<StartupEntry>().ToList();
        if (selected.Count == 0) return;

        var result = MessageBox.Show(
            $"선택한 {selected.Count}개 항목을 시작 프로그램에서 삭제하시겠습니까?",
            "삭제 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        int deleted = 0;
        foreach (var entry in selected)
        {
            if (_service.Delete(entry))
            {
                _allEntries.Remove(entry);
                deleted++;
            }
        }

        ApplyFilter();
        TbStatus.Text = $"{deleted}개 항목 삭제됨";
    }
}
