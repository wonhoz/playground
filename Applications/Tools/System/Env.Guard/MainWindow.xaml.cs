using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnvGuard.Models;
using EnvGuard.Services;

namespace EnvGuard;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private List<EnvVariable> _allVars = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        if (EnvService.IsAdmin())
            TxtAdmin.Visibility = Visibility.Visible;

        LoadVariables();
        await LoadPathEntriesAsync();
    }

    // ── 변수 로딩 ──────────────────────────────────

    private void LoadVariables()
    {
        _allVars = EnvService.GetAll();
        ApplyFilter();
        TxtStatus.Text = $"환경변수 {_allVars.Count}개 로드됨";
    }

    private void ApplyFilter()
    {
        if (!IsLoaded) return;
        var search = TxtSearch.Text.Trim();
        var filtered = _allVars.AsEnumerable();

        if (RbUser.IsChecked == true)
            filtered = filtered.Where(v => v.Scope == EnvScope.User);
        else if (RbSystem.IsChecked == true)
            filtered = filtered.Where(v => v.Scope == EnvScope.System);

        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(v =>
                v.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                v.Value.Contains(search, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();
        DgVars.ItemsSource = list;
        TxtCount.Text = $"{list.Count}/{_allVars.Count}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadVariables();
        await LoadPathEntriesAsync();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ScopeFilter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    // ── 변수 편집 ──────────────────────────────────

    private void DgVars_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DgVars.SelectedItem is not EnvVariable v) return;
        TxtName.Text = v.Name;
        TxtValue.Text = v.Value;
        CmbScope.SelectedIndex = v.Scope == EnvScope.User ? 0 : 1;
    }

    private void SaveVar_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        var value = TxtValue.Text;
        if (string.IsNullOrEmpty(name))
        {
            ShowMessage("변수명을 입력해주세요.", true);
            return;
        }

        var scope = CmbScope.SelectedIndex == 0 ? EnvScope.User : EnvScope.System;

        if (scope == EnvScope.System && !EnvService.IsAdmin())
        {
            ShowMessage("시스템 변수 수정은 관리자 권한이 필요합니다.", true);
            return;
        }

        // Auto snapshot before change
        SnapshotService.CreateSnapshot($"자동 - {name} 변경 전", _allVars);

        try
        {
            EnvService.SetVariable(name, value, scope);
            LoadVariables();
            ShowMessage($"'{name}' 저장 완료 (자동 스냅샷 생성됨)");
        }
        catch (Exception ex)
        {
            ShowMessage($"저장 실패: {ex.Message}", true);
        }
    }

    private void DeleteVar_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var scope = CmbScope.SelectedIndex == 0 ? EnvScope.User : EnvScope.System;

        if (scope == EnvScope.System && !EnvService.IsAdmin())
        {
            ShowMessage("시스템 변수 삭제는 관리자 권한이 필요합니다.", true);
            return;
        }

        var result = MessageBox.Show($"'{name}' ({scope}) 변수를 삭제하시겠습니까?\n삭제 전 자동 스냅샷이 생성됩니다.",
            "변수 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        SnapshotService.CreateSnapshot($"자동 - {name} 삭제 전", _allVars);

        try
        {
            EnvService.DeleteVariable(name, scope);
            TxtName.Text = "";
            TxtValue.Text = "";
            LoadVariables();
            ShowMessage($"'{name}' 삭제 완료");
        }
        catch (Exception ex)
        {
            ShowMessage($"삭제 실패: {ex.Message}", true);
        }
    }

    // ── PATH 편집기 ──────────────────────────────────

    private async Task LoadPathEntriesAsync()
    {
        await LoadPathListAsync(LbUserPath, EnvScope.User);
        await LoadPathListAsync(LbSysPath, EnvScope.System);
    }

    // UI 차단 방지: Directory.Exists()를 배경 스레드에서 일괄 수행 (네트워크 드라이브 hang 방지)
    private async Task LoadPathListAsync(ListBox lb, EnvScope scope)
    {
        lb.Items.Clear();
        var entries = EnvService.GetPathEntries(scope);

        TxtStatus.Text = "PATH 경로 확인 중...";
        var results = await Task.Run(() =>
            entries.Select(e => (entry: e, exists: Directory.Exists(e))).ToList());

        foreach (var (entry, exists) in results)
        {
            var item = new ListBoxItem
            {
                Content    = entry,
                Foreground = exists
                    ? new SolidColorBrush(Color.FromRgb(80, 220, 120))
                    : new SolidColorBrush(Color.FromRgb(255, 90, 90)),
                ToolTip    = exists ? "✅ 경로 존재" : "❌ 경로 없음",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Padding    = new Thickness(6, 3, 6, 3)
            };
            lb.Items.Add(item);
        }
    }

    private void PathList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var (lb, scope) = GetPathListFromTag(sender);
        if (lb.SelectedIndex <= 0) return;

        SnapshotService.CreateSnapshot($"자동 - PATH 순서 변경 전", _allVars);

        var entries = GetPathItems(lb);
        var idx = lb.SelectedIndex;
        (entries[idx - 1], entries[idx]) = (entries[idx], entries[idx - 1]);
        EnvService.SetPathEntries(entries, scope);
        await LoadPathListAsync(lb, scope);
        lb.SelectedIndex = idx - 1;
        LoadVariables();
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var (lb, scope) = GetPathListFromTag(sender);
        if (lb.SelectedIndex < 0 || lb.SelectedIndex >= lb.Items.Count - 1) return;

        SnapshotService.CreateSnapshot($"자동 - PATH 순서 변경 전", _allVars);

        var entries = GetPathItems(lb);
        var idx = lb.SelectedIndex;
        (entries[idx + 1], entries[idx]) = (entries[idx], entries[idx + 1]);
        EnvService.SetPathEntries(entries, scope);
        await LoadPathListAsync(lb, scope);
        lb.SelectedIndex = idx + 1;
        LoadVariables();
    }

    private async void AddPath_Click(object sender, RoutedEventArgs e)
    {
        var (lb, scope) = GetPathListFromTag(sender);

        if (scope == EnvScope.System && !EnvService.IsAdmin())
        {
            ShowMessage("시스템 PATH 수정은 관리자 권한이 필요합니다.", true);
            return;
        }

        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "PATH에 추가할 폴더 선택" };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        SnapshotService.CreateSnapshot($"자동 - PATH 추가 전", _allVars);

        var entries = GetPathItems(lb);
        if (!entries.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(dlg.SelectedPath);
            EnvService.SetPathEntries(entries, scope);
            await LoadPathListAsync(lb, scope);
            LoadVariables();
            ShowMessage($"PATH에 '{dlg.SelectedPath}' 추가됨");
        }
        else
        {
            ShowMessage("이미 PATH에 존재하는 경로입니다.", true);
        }
    }

    private async void RemovePath_Click(object sender, RoutedEventArgs e)
    {
        var (lb, scope) = GetPathListFromTag(sender);
        if (lb.SelectedIndex < 0) return;

        if (scope == EnvScope.System && !EnvService.IsAdmin())
        {
            ShowMessage("시스템 PATH 수정은 관리자 권한이 필요합니다.", true);
            return;
        }

        var entries = GetPathItems(lb);
        var removed = entries[lb.SelectedIndex];

        var result = MessageBox.Show($"PATH에서 '{removed}'를 제거하시겠습니까?",
            "PATH 항목 제거", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        SnapshotService.CreateSnapshot($"자동 - PATH 제거 전", _allVars);

        entries.RemoveAt(lb.SelectedIndex);
        EnvService.SetPathEntries(entries, scope);
        await LoadPathListAsync(lb, scope);
        LoadVariables();
        ShowMessage($"PATH에서 '{removed}' 제거됨");
    }

    private (ListBox lb, EnvScope scope) GetPathListFromTag(object sender)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        return tag == "System" ? (LbSysPath, EnvScope.System) : (LbUserPath, EnvScope.User);
    }

    private static List<string> GetPathItems(ListBox lb)
    {
        return lb.Items.OfType<ListBoxItem>().Select(i => i.Content.ToString() ?? "").ToList();
    }

    // ── 스냅샷 ──────────────────────────────────

    private void SaveSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var desc = Microsoft.VisualBasic.Interaction.InputBox(
            "스냅샷 설명을 입력해주세요:", "스냅샷 저장", $"수동 스냅샷 {DateTime.Now:yyyy-MM-dd HH:mm}");

        if (string.IsNullOrWhiteSpace(desc)) return;

        SnapshotService.CreateSnapshot(desc, _allVars);
        ShowMessage("스냅샷 저장 완료");
    }

    private async void ListSnapshots_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SnapshotDialog(_allVars) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            LoadVariables();
            await LoadPathEntriesAsync();
            ShowMessage("스냅샷 복원 완료");
        }
    }

    // ── 유틸 ──────────────────────────────────

    private void ShowMessage(string msg, bool isError = false)
    {
        TxtStatus.Text = msg;
        TxtStatus.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(255, 90, 90))
            : new SolidColorBrush(Color.FromRgb(80, 220, 120));
    }
}
