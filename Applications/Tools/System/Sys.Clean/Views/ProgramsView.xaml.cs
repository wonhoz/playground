using System.Windows;
using System.Windows.Controls;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class ProgramsView : UserControl
{
    private readonly ProgramService _service = new();
    private List<InstalledProgram> _allPrograms = [];

    public ProgramsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        _allPrograms = _service.GetInstalledPrograms();
        UpdateStats();
        ApplyFilter();
    }

    private void UpdateStats()
    {
        TbProgramCount.Text = $"총 {_allPrograms.Count}개";
        var totalSize = _allPrograms.Sum(p => p.SizeBytes);
        TbTotalSize.Text = totalSize > 0 ? $"— {CleanTarget.FormatSize(totalSize)}" : "";
    }

    private void ApplyFilter()
    {
        if (!IsLoaded) return;

        var search = TxtSearch?.Text.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(search)
            ? _allPrograms.AsEnumerable()
            : _allPrograms.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Publisher.ToLower().Contains(search));

        var sortIdx = CbSort?.SelectedIndex ?? 0;
        var sorted = sortIdx switch
        {
            1 => filtered.OrderByDescending(p => p.SizeBytes),
            2 => filtered.OrderByDescending(p => p.InstallDate),
            3 => filtered.OrderBy(p => p.Publisher).ThenBy(p => p.Name),
            _ => filtered.OrderBy(p => p.Name),
        };

        ProgramList.ItemsSource = sorted.ToList();
        TbStatus.Text = $"{ProgramList.Items.Count}개 표시 중";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void CbSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (ProgramList.SelectedItem is not InstalledProgram program) return;

        if (string.IsNullOrEmpty(program.UninstallString))
        {
            MessageBox.Show("이 프로그램의 제거 명령을 찾을 수 없습니다.", "오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"'{program.Name}'을 제거하시겠습니까?\n\n제거 프로그램이 실행됩니다.",
            "제거 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK) return;

        if (_service.Uninstall(program))
        {
            TbStatus.Text = $"'{program.Name}' 제거 프로세스 시작됨";
        }
        else
        {
            MessageBox.Show("제거 프로그램을 실행할 수 없습니다.", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
