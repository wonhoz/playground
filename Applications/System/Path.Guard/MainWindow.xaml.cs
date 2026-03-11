namespace PathGuard;

public partial class MainWindow : Window
{
    // ── 데이터 ──────────────────────────────────────────────────────────
    private readonly ObservableCollection<PathEntry> _all      = [];
    private readonly ObservableCollection<PathEntry> _filtered = [];
    private PathEntry? _selected;
    private Point _dragStart;
    private PathEntry? _dragging;

    // ── 다크 타이틀바 ────────────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow()
    {
        InitializeComponent();
        LstPath.ItemsSource      = _filtered;
        LstSnapshots.ItemsSource = SnapshotService.Snapshots;

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

            // 관리자 권한 체크
            bool isAdmin = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            TxtAdmin.Text = isAdmin ? "" : "⚠ 관리자 권한 없음 (시스템 PATH 변경 불가)";

            Reload();
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // 로드 / 필터
    // ──────────────────────────────────────────────────────────────────

    private void Reload()
    {
        _all.Clear();
        foreach (var e in PathService.Load()) _all.Add(e);
        ApplyFilter();
        UpdateSummary();
        SetStatus($"PATH 로드 완료 — {_all.Count}개 항목");
    }

    private void ApplyFilter()
    {
        if (!IsLoaded) return;
        bool showSys  = ChkSystem.IsChecked == true;
        bool showUser = ChkUser.IsChecked   == true;
        bool onlyBroken = ChkBroken.IsChecked == true;

        _filtered.Clear();
        foreach (var e in _all)
        {
            if (e.Scope == PathScope.System && !showSys)  continue;
            if (e.Scope == PathScope.User   && !showUser) continue;
            if (onlyBroken && e.Status == PathStatus.Ok)  continue;
            _filtered.Add(e);
        }
        TxtSummary.Text = $"{_filtered.Count} / {_all.Count}개 표시";
    }

    private void UpdateSummary()
    {
        int broken    = _all.Count(e => e.Status == PathStatus.Broken);
        int duplicate = _all.Count(e => e.Status == PathStatus.Duplicate);
        int disabled  = _all.Count(e => !e.IsEnabled);
        SetStatus($"총 {_all.Count}개  |  깨짐 {broken}  |  중복 {duplicate}  |  비활성 {disabled}");
    }

    private void ChkFilter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void BtnReload_Click(object sender, RoutedEventArgs e) => Reload();

    // ──────────────────────────────────────────────────────────────────
    // 저장
    // ──────────────────────────────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("변경사항을 PATH 환경변수에 적용하시겠습니까?\n현재 실행 중인 프로그램에는 즉시 반영되지 않습니다.",
            "변경 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            PathService.Save(_all);
            SetStatus("✓ PATH 적용 완료 (WM_SETTINGCHANGE 브로드캐스트)");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException)
        {
            MessageBox.Show("시스템 PATH 변경은 관리자 권한이 필요합니다.\n관리자로 재실행하세요.",
                "권한 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // 항목 편집
    // ──────────────────────────────────────────────────────────────────

    private void LstPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = LstPath.SelectedItem as PathEntry;
        bool hasSel = _selected != null;
        BtnDelete.IsEnabled   = hasSel;
        BtnToggle.IsEnabled   = hasSel;
        BtnMoveUp.IsEnabled   = hasSel && _filtered.IndexOf(_selected!) > 0;
        BtnMoveDown.IsEnabled = hasSel && _filtered.IndexOf(_selected!) < _filtered.Count - 1;

        if (_selected != null)
        {
            TxtEditPath.Text = _selected.RawValue;
            TxtExpandPreview.Text = _selected.ExpandedValue != _selected.RawValue
                ? "→ " + _selected.ExpandedValue : "";
        }
    }

    private void TxtEditPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var expanded = Environment.ExpandEnvironmentVariables(TxtEditPath.Text);
        TxtExpandPreview.Text = expanded != TxtEditPath.Text ? "→ " + expanded : "";
    }

    private void BtnApplyEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var newPath = TxtEditPath.Text.Trim();
        if (string.IsNullOrEmpty(newPath)) return;
        _selected.RawValue      = newPath;
        _selected.ExpandedValue = Environment.ExpandEnvironmentVariables(newPath);
        PathService.Diagnose(_all.ToList());
        LstPath.Items.Refresh();
        UpdateSummary();
    }

    private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddPathDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var newEntry = new PathEntry
        {
            RawValue      = dlg.PathValue,
            ExpandedValue = Environment.ExpandEnvironmentVariables(dlg.PathValue),
            Scope         = dlg.IsSystem ? PathScope.System : PathScope.User,
            IsEnabled     = true
        };
        PathService.Diagnose([.. _all, newEntry]);
        _all.Add(newEntry);
        ApplyFilter();
        LstPath.SelectedItem = newEntry;
        SetStatus($"항목 추가: {newEntry.RawValue}");
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show($"'{_selected.RawValue}' 항목을 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _all.Remove(_selected);
        ApplyFilter();
        PathService.Diagnose(_all.ToList());
        UpdateSummary();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.IsEnabled = !_selected.IsEnabled;
        PathService.Diagnose(_all.ToList());
        LstPath.Items.Refresh();
        UpdateSummary();
    }

    // ──────────────────────────────────────────────────────────────────
    // 이동
    // ──────────────────────────────────────────────────────────────────

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)    => MoveSelected(-1);
    private void BtnMoveDown_Click(object sender, RoutedEventArgs e)   => MoveSelected(+1);

    private void MoveSelected(int delta)
    {
        if (_selected == null) return;
        int idxAll = _all.IndexOf(_selected);
        int newIdx = idxAll + delta;
        if (newIdx < 0 || newIdx >= _all.Count) return;
        _all.Move(idxAll, newIdx);
        ApplyFilter();
        LstPath.SelectedItem = _selected;
        LstPath.ScrollIntoView(_selected);
    }

    // ──────────────────────────────────────────────────────────────────
    // 드래그&드롭 재정렬
    // ──────────────────────────────────────────────────────────────────

    private void LstPath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(LstPath);
        _dragging = (LstPath.SelectedItem as PathEntry);
    }

    private void LstPath_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging == null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(LstPath);
        if (Math.Abs(pos.X - _dragStart.X) < 4 && Math.Abs(pos.Y - _dragStart.Y) < 4) return;
        DragDrop.DoDragDrop(LstPath, _dragging, DragDropEffects.Move);
        _dragging = null;
    }

    private void LstPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PathEntry)) is not PathEntry dragItem) return;
        var target = GetItemUnderPoint(e.GetPosition(LstPath));
        if (target == null || target == dragItem) return;

        int fromAll = _all.IndexOf(dragItem);
        int toAll   = _all.IndexOf(target);
        if (fromAll < 0 || toAll < 0) return;
        _all.Move(fromAll, toAll);
        ApplyFilter();
        LstPath.SelectedItem = dragItem;
    }

    private PathEntry? GetItemUnderPoint(Point pt)
    {
        var element = LstPath.InputHitTest(pt) as DependencyObject;
        while (element != null)
        {
            if (element is ListBoxItem item) return item.DataContext as PathEntry;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────
    // 실행파일 검색 + 버전 충돌
    // ──────────────────────────────────────────────────────────────────

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) RunSearch(); }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => RunSearch();

    private void RunSearch()
    {
        var query = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // 확장자 없으면 .exe 자동 추가
        if (!query.Contains('*') && !Path.HasExtension(query)) query += ".exe";

        PathService.SearchExecutable(_all.ToList(), query);
        LstPath.Items.Refresh();

        var hits = _all.Where(e => e.HasHits).ToList();
        TxtHits.Text = hits.Count > 0 ? $"🔍 {hits.Count}개 경로에서 발견" : "검색 결과 없음";

        // 버전 충돌 경고 (2개 이상 경로에서 동일 파일 발견)
        if (hits.Count >= 2)
        {
            BdrConflict.Visibility = Visibility.Visible;
            TxtConflict.Text = $"'{query}' 파일이 {hits.Count}개 경로에 존재합니다.\n" +
                               "상위 경로의 버전이 우선 실행됩니다.\n" +
                               string.Join("\n", hits.Take(5).Select((h, i) => $"  {i + 1}. {h.ExpandedValue}"));
        }
        else
        {
            BdrConflict.Visibility = Visibility.Collapsed;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // 스냅샷
    // ──────────────────────────────────────────────────────────────────

    private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var snap = SnapshotService.Save();
        (LstSnapshots.ItemsSource as System.Collections.IList)?.Clear();
        LstSnapshots.ItemsSource = null;
        LstSnapshots.ItemsSource = SnapshotService.Snapshots;
        SetStatus($"스냅샷 저장: {snap.Label} ({snap.CreatedAt:HH:mm:ss})");
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (LstSnapshots.SelectedItem is not PathSnapshot snap)
        { MessageBox.Show("롤백할 스냅샷을 선택하세요.", "선택 없음"); return; }
        if (MessageBox.Show($"'{snap.Label}' 스냅샷으로 PATH를 복원하시겠습니까?",
            "롤백 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            SnapshotService.Restore(snap);
            Reload();
            SetStatus($"롤백 완료: {snap.Label}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"롤백 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LstSnapshots_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnRestore_Click(sender, new RoutedEventArgs());
    }

    // ──────────────────────────────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────────────────────────────

    private void SetStatus(string msg) => TxtStatus.Text = msg;
}

// ══════════════════════════════════════════════════════════════════════
// 항목 추가 다이얼로그
// ══════════════════════════════════════════════════════════════════════

public partial class AddPathDialog : Window
{
    public string PathValue { get; private set; } = "";
    public bool   IsSystem  { get; private set; } = false;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public AddPathDialog()
    {
        Title = "PATH 항목 추가";
        Width = 520; Height = 220;
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new StackPanel { Margin = new Thickness(20) };
        Content = root;

        root.Children.Add(new TextBlock { Text = "경로 또는 환경변수 표현식", Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
        var txt = new TextBox { Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
        var preview = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), FontSize = 10, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 3, 0, 10), TextTrimming = TextTrimming.CharacterEllipsis };
        txt.TextChanged += (_, _) => { var exp = Environment.ExpandEnvironmentVariables(txt.Text); preview.Text = exp != txt.Text ? "→ " + exp : ""; };
        root.Children.Add(txt); root.Children.Add(preview);

        var scopeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var rbUser   = new RadioButton { Content = "사용자 PATH", Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)), IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        var rbSystem = new RadioButton { Content = "시스템 PATH", Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)) };
        scopeRow.Children.Add(rbUser); scopeRow.Children.Add(rbSystem);
        root.Children.Add(scopeRow);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "취소", Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 0, 6, 0), Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x2A, 0x36)), Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x4A)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        cancel.Template = CreateButtonTemplate();
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "추가", Padding = new Thickness(14, 5, 14, 5), Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x1A, 0x2A)), Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x4A)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        ok.Template = CreateButtonTemplate();
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) { MessageBox.Show("경로를 입력하세요."); return; }
            PathValue = txt.Text.Trim(); IsSystem = rbSystem.IsChecked == true;
            DialogResult = true; Close();
        };
        btns.Children.Add(cancel); btns.Children.Add(ok);
        root.Children.Add(btns);

        Loaded += (_, _) => { var h = new WindowInteropHelper(this).Handle; int d = 1; DwmSetWindowAttribute(h, 20, ref d, sizeof(int)); txt.Focus(); };
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "Bd";
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        t.VisualTree = bd;
        return t;
    }
}
