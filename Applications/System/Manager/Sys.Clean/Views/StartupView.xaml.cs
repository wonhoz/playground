using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class StartupView : UserControl
{
    private readonly StartupService _service = new();
    private List<StartupEntry> _allEntries = [];

    private GridViewColumnHeader? _sortHeader;
    private string? _sortColumn;
    private bool _sortAscending = true;

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
        ApplySort();
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
        if (_sortColumn == null || StartupList.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(StartupList.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortColumn,
            _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
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

    // ── 추가 폼 토글 ──────────────────────────────────────────────────
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        bool isVisible = AddPanel.Visibility == Visibility.Visible;
        AddPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        if (!isVisible)
        {
            TxtAddName.Text = "";
            TxtAddCommand.Text = "";
            CbAddLocation.SelectedIndex = 1; // HKCU 기본
            TxtAddName.Focus();
        }
    }

    private void BtnAddCancel_Click(object sender, RoutedEventArgs e)
    {
        AddPanel.Visibility = Visibility.Collapsed;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "실행 파일 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            TxtAddCommand.Text = $"\"{dlg.FileName}\"";
    }

    private void BtnAddSave_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtAddName.Text.Trim();
        var command = TxtAddCommand.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("이름을 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAddName.Focus();
            return;
        }
        if (string.IsNullOrEmpty(command))
        {
            MessageBox.Show("명령(실행 파일 경로)을 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAddCommand.Focus();
            return;
        }

        var location = CbAddLocation.SelectedIndex == 0
            ? StartupLocation.HklmRun
            : StartupLocation.HkcuRun;

        if (_service.Add(name, command, location))
        {
            AddPanel.Visibility = Visibility.Collapsed;
            Refresh();
            TbStatus.Text = $"'{name}' 시작 프로그램에 추가됨";
        }
        else
        {
            MessageBox.Show("추가에 실패했습니다. HKLM(모든 사용자)은 관리자 권한이 필요합니다.",
                "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
