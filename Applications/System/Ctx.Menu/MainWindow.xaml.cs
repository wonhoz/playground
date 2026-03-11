using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;

namespace CtxMenu;

// ── 사이드바 카테고리 항목 ────────────────────────────────────────
public class SideCategory
{
    public string Label    { get; set; } = "";
    public string Icon     { get; set; } = "";
    public TargetType? Type { get; set; }      // null = 전체
    public string? Ext     { get; set; }       // Extension 타입일 때
    public bool IsHeader   { get; set; }
    public int Count       { get; set; }

    public string Display  => IsHeader ? Label : $"{Icon}  {Label}";
    public string CountStr => Count > 0 ? Count.ToString() : "";
}

// ── MainWindow ────────────────────────────────────────────────────
public partial class MainWindow : Window
{
    private List<ShellEntry>              _all      = [];
    private ObservableCollection<ShellEntry> _view  = [];
    private List<SideCategory>            _cats     = [];
    private SideCategory?                 _selCat;

    public MainWindow()
    {
        InitializeComponent();
        Grid.ItemsSource = _view;

        Loaded += async (_, _) =>
        {
            ShowAdminBadge();
            await LoadAsync();
        };
    }

    // ── 관리자 권한 ────────────────────────────────────────────────
    private static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void ShowAdminBadge() =>
        AdminBadge.Visibility = IsAdmin() ? Visibility.Collapsed : Visibility.Visible;

    // ── 로딩 ───────────────────────────────────────────────────────
    private async Task LoadAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        BtnRefresh.IsEnabled      = false;

        var progress = new Progress<string>(msg => TxtLoadCount.Text = msg);
        _all = await Task.Run(() => RegistryService.LoadAll(progress));

        RebuildSidebar();
        ApplyFilter();
        UpdateStatus();

        LoadingOverlay.Visibility = Visibility.Collapsed;
        BtnRefresh.IsEnabled      = true;
    }

    // ── 사이드바 구성 ──────────────────────────────────────────────
    private void RebuildSidebar()
    {
        _cats = [];

        _cats.Add(new SideCategory { Label = "전체", Icon = "◈",
            Count = _all.Count });

        _cats.Add(new SideCategory { Label = "─────────", IsHeader = true });

        _cats.Add(new SideCategory { Label = "모든 파일", Icon = "★",
            Type = TargetType.AllFiles,
            Count = _all.Count(e => e.TargetType == TargetType.AllFiles) });

        _cats.Add(new SideCategory { Label = "폴더", Icon = "📁",
            Type = TargetType.Folder,
            Count = _all.Count(e => e.TargetType == TargetType.Folder) });

        _cats.Add(new SideCategory { Label = "배경", Icon = "🖥",
            Type = TargetType.Background,
            Count = _all.Count(e => e.TargetType == TargetType.Background) });

        _cats.Add(new SideCategory { Label = "드라이브", Icon = "💾",
            Type = TargetType.Drive,
            Count = _all.Count(e => e.TargetType == TargetType.Drive) });

        var extEntries = _all.Where(e => e.TargetType == TargetType.Extension).ToList();
        if (extEntries.Count > 0)
        {
            _cats.Add(new SideCategory { Label = "─────────", IsHeader = true });
            _cats.Add(new SideCategory { Label = "확장자별", IsHeader = true });

            foreach (var ext in extEntries.Select(e => e.ExtFilter)
                         .Distinct().OrderBy(x => x))
            {
                _cats.Add(new SideCategory
                {
                    Label = ext,
                    Icon  = "·",
                    Type  = TargetType.Extension,
                    Ext   = ext,
                    Count = extEntries.Count(e => e.ExtFilter == ext),
                });
            }
        }

        // 사이드바 ListBox 재구성
        SideList.Items.Clear();
        foreach (var cat in _cats)
        {
            var item = new ListBoxItem
            {
                Content = BuildSideCatContent(cat),
                Tag     = cat,
                Style   = cat.IsHeader
                    ? (Style)FindResource("SidebarHeaderItem")
                    : (Style)FindResource("SidebarItem"),
            };
            SideList.Items.Add(item);
        }

        // 첫 번째 항목(전체) 선택
        if (SideList.Items.Count > 0)
            ((ListBoxItem)SideList.Items[0]).IsSelected = true;
    }

    private static FrameworkElement BuildSideCatContent(SideCategory cat)
    {
        if (cat.IsHeader)
            return new TextBlock { Text = cat.Label, FontSize = 10 };

        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text = cat.Display,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        System.Windows.Controls.Grid.SetColumn(lbl, 0);

        var cnt = new TextBlock
        {
            Text = cat.CountStr,
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        System.Windows.Controls.Grid.SetColumn(cnt, 1);

        grid.Children.Add(lbl);
        grid.Children.Add(cnt);
        return grid;
    }

    // ── 필터 적용 ─────────────────────────────────────────────────
    private void ApplyFilter()
    {
        if (!IsLoaded) return;

        var search      = TxtSearch?.Text.Trim() ?? "";
        var scopeIdx    = CmbScope?.SelectedIndex ?? 0;
        var showDisabled = ChkShowDisabled?.IsChecked ?? true;

        IEnumerable<ShellEntry> filtered = _all;

        // 카테고리 필터
        if (_selCat?.Type != null)
        {
            if (_selCat.Ext != null)
                filtered = filtered.Where(e => e.TargetType == TargetType.Extension && e.ExtFilter == _selCat.Ext);
            else
                filtered = filtered.Where(e => e.TargetType == _selCat.Type);
        }

        // 범위 필터
        if (scopeIdx == 1) filtered = filtered.Where(e => e.Scope == RegistryScope.System);
        else if (scopeIdx == 2) filtered = filtered.Where(e => e.Scope == RegistryScope.User);

        // 비활성 숨기기
        if (showDisabled == false)
            filtered = filtered.Where(e => e.IsEnabled);

        // 검색
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(e =>
                e.DisplayOrKey.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.KeyName.Contains(search, StringComparison.OrdinalIgnoreCase)      ||
                e.Command.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _view.Clear();
        foreach (var e in filtered.OrderBy(e => e.DisplayOrKey))
            _view.Add(e);

        // 플레이스홀더 / 빈 목록
        if (PlaceholderSearch != null)
            PlaceholderSearch.Visibility = string.IsNullOrEmpty(TxtSearch?.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        if (BtnClearSearch != null)
            BtnClearSearch.Visibility = string.IsNullOrEmpty(TxtSearch?.Text)
                ? Visibility.Collapsed : Visibility.Visible;

        EmptyOverlay.Visibility = _view.Count == 0 && !string.IsNullOrEmpty(search)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 상태 바 업데이트 ───────────────────────────────────────────
    private void UpdateStatus()
    {
        TxtCount.Text  = $"{_all.Count}개 항목";
        TxtStatus.Text = $"표시: {_view.Count} / 전체: {_all.Count}  |  " +
                         $"활성: {_all.Count(e => e.IsEnabled)}  비활성: {_all.Count(e => !e.IsEnabled)}";
    }

    // ── UI 이벤트 ─────────────────────────────────────────────────
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = "";
        TxtSearch.Focus();
    }

    private void CmbScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) =>
        await LoadAsync();

    private void SideList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (SideList.SelectedItem is ListBoxItem { Tag: SideCategory cat })
        {
            _selCat = cat;
            ApplyFilter();
            UpdateStatus();
        }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = Grid.SelectedItem as ShellEntry;
        BtnEdit.IsEnabled   = sel != null;
        BtnDelete.IsEnabled = sel != null;
        TxtSelected.Text    = sel != null ? $"선택: {sel.RegistryPath}" : "";
    }

    // ── 활성화 체크박스 ────────────────────────────────────────────
    private void ChkEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox chk || chk.Tag is not ShellEntry entry) return;

        try
        {
            var newState = chk.IsChecked == true;
            RegistryService.SetEnabled(entry, newState);
            ShellNotifyService.NotifyAssocChanged();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"변경 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            // 체크박스 상태 복원
            chk.IsChecked = entry.IsEnabled;
        }
    }

    // ── 추가 ───────────────────────────────────────────────────────
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditDialog(this);
        if (dlg.ShowDialog() != true) return;

        try
        {
            RegistryService.SaveEntry(dlg.Result);
            _all.Add(dlg.Result);
            ShellNotifyService.NotifyAssocChanged();
            RebuildSidebar();
            ApplyFilter();
            UpdateStatus();

            // 추가된 항목 선택
            Grid.SelectedItem = dlg.Result;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"추가 실패: {ex.Message}\n\n관리자 권한으로 실행하세요.",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 편집 ───────────────────────────────────────────────────────
    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ShellEntry entry) return;

        var dlg = new EditDialog(this, entry);
        if (dlg.ShowDialog() != true) return;

        try
        {
            // RegistryPath에 원래 키이름 임시 저장됨
            var originalKey = dlg.Result.RegistryPath;
            dlg.Result.RegistryPath = entry.RegistryPath; // 원래 경로 복원
            dlg.Result.IsEnabled    = entry.IsEnabled;

            RegistryService.SaveEntry(dlg.Result,
                originalKey != dlg.Result.KeyName ? originalKey : null);

            // _all 목록 교체
            var idx = _all.IndexOf(entry);
            if (idx >= 0) _all[idx] = dlg.Result;

            ShellNotifyService.NotifyAssocChanged();
            RebuildSidebar();
            ApplyFilter();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"편집 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 삭제 ───────────────────────────────────────────────────────
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ShellEntry entry) return;

        var msg = $"'{entry.DisplayOrKey}' 항목을 삭제하시겠습니까?\n\n{entry.RegistryPath}";
        if (MessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;

        try
        {
            RegistryService.DeleteEntry(entry);
            _all.Remove(entry);
            ShellNotifyService.NotifyAssocChanged();
            RebuildSidebar();
            ApplyFilter();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 백업 ───────────────────────────────────────────────────────
    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "백업 파일 저장",
            Filter           = "JSON 파일 (*.json)|*.json",
            FileName         = $"ctx-menu-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExt       = ".json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var json = RegistryService.ExportJson(_all);
            File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
            MessageBox.Show($"백업 완료: {dlg.FileName}\n\n{_all.Count}개 항목이 저장되었습니다.",
                "백업", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"백업 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 복원 ───────────────────────────────────────────────────────
    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "백업 파일 열기",
            Filter = "JSON 파일 (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var json    = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            var entries = RegistryService.ImportJson(json);
            if (entries == null || entries.Count == 0)
            {
                MessageBox.Show("백업 파일에 항목이 없거나 형식이 올바르지 않습니다.",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"{entries.Count}개 항목을 복원하시겠습니까?\n이미 존재하는 항목은 덮어씌워집니다.",
                "복원 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int ok = 0, fail = 0;
            foreach (var entry in entries)
            {
                try
                {
                    RegistryService.SaveEntry(entry);
                    ok++;
                }
                catch { fail++; }
            }

            ShellNotifyService.NotifyAssocChanged();
            MessageBox.Show($"복원 완료: 성공 {ok}개 / 실패 {fail}개",
                "복원", MessageBoxButton.OK, MessageBoxImage.Information);

            _ = LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"복원 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 비활성 표시 토글 ───────────────────────────────────────────
    private void ChkShowDisabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }
}
