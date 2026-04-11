using System.Windows;
using System.Windows.Controls;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class ProgramsView : UserControl
{
    private readonly ProgramService _service = new();
    private List<InstalledProgram> _allPrograms = [];

    private GridViewColumnHeader? _sortHeader;
    private string? _sortColumn;
    private bool _sortAscending = true;

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
            ? _allPrograms
            : _allPrograms.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Publisher.ToLower().Contains(search)).ToList();

        ProgramList.ItemsSource = filtered;
        ApplySort();
        TbStatus.Text = $"{ProgramList.Items.Count}개 표시 중";
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
        if (_sortColumn == null || ProgramList.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(ProgramList.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(_sortColumn,
            _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_allPrograms.Count == 0)
        {
            DarkMessageBox.Show("내보낼 프로그램이 없습니다.", "내보내기",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "프로그램 목록 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = $"installed_programs_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("이름,제조사,버전,크기,설치일");
            foreach (var p in _allPrograms)
            {
                var name = CsvEscape(p.Name);
                var pub = CsvEscape(p.Publisher);
                var ver = CsvEscape(p.Version);
                sb.AppendLine($"{name},{pub},{ver},{p.SizeText},{p.InstallDate}");
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            TbStatus.Text = $"CSV 내보내기 완료: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show($"내보내기 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string CsvEscape(string s)
        => s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        var selected = ProgramList.SelectedItems.Cast<InstalledProgram>().ToList();
        if (selected.Count == 0) return;

        // 제거 문자열이 없는 항목 필터링
        var canUninstall = selected.Where(p => !string.IsNullOrEmpty(p.UninstallString)).ToList();
        var noUninstaller = selected.Count - canUninstall.Count;

        if (canUninstall.Count == 0)
        {
            DarkMessageBox.Show("선택한 프로그램의 제거 명령을 찾을 수 없습니다.", "오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var msg = canUninstall.Count == 1
            ? $"'{canUninstall[0].Name}'을 제거하시겠습니까?\n\n제거 프로그램이 실행됩니다."
            : $"선택한 {canUninstall.Count}개 프로그램을 순차적으로 제거하시겠습니까?\n\n각 프로그램의 제거 창이 순서대로 실행됩니다.";

        if (noUninstaller > 0)
            msg += $"\n\n※ 제거 명령 없음 {noUninstaller}개는 건너뜁니다.";

        var result = DarkMessageBox.Show(msg, "제거 확인", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        int succeeded = 0;
        foreach (var program in canUninstall)
        {
            if (_service.Uninstall(program))
                succeeded++;
            else
                DarkMessageBox.Show($"'{program.Name}' 제거 프로그램을 실행할 수 없습니다.", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
        }

        TbStatus.Text = succeeded > 0
            ? $"{succeeded}개 제거 완료 — 레지스트리 탭에서 잔여 항목을 정리하세요."
            : "제거에 실패했습니다.";
    }
}
