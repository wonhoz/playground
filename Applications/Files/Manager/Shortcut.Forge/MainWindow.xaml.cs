using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace ShortcutForge;

public partial class MainWindow : Window
{
    // ── 데이터 ──────────────────────────────────────────────────────────
    private readonly ObservableCollection<ShortcutEntry> _all      = [];
    private readonly ObservableCollection<ShortcutEntry> _filtered = [];
    private ShortcutEntry? _editing;
    private bool _isDirty;
    private string _currentScanDir = "";

    // ── 다크 타이틀바 P/Invoke ──────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow()
    {
        InitializeComponent();
        LstShortcuts.ItemsSource = _filtered;
        PopulateQuickLocations();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            SetStatus("폴더를 스캔하거나 새 바로가기를 추가하세요.");
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // 스캔
    // ──────────────────────────────────────────────────────────────────

    private void BtnScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "스캔할 폴더 선택" };
        if (dlg.ShowDialog(this) != true) return;
        ScanFolder(dlg.FolderName, recurse: false);
    }

    private void BtnScanDesktop_Click(object sender, RoutedEventArgs e)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        ScanFolder(desktop, recurse: false);
    }

    private void BtnScanStartMenu_Click(object sender, RoutedEventArgs e)
    {
        var sm = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        ScanFolder(sm, recurse: true);
    }

    private async void ScanFolder(string folder, bool recurse)
    {
        SetStatus($"스캔 중: {folder}");
        _currentScanDir = folder;
        TxtScanDir.Text = folder;

        _all.Clear();
        var entries = await Task.Run(() => ShortcutScanner.Scan(folder, recurse).ToList());
        foreach (var e in entries) _all.Add(e);

        ApplyFilter();
        SetStatus($"스캔 완료 — {_all.Count}개 바로가기");
    }

    // ──────────────────────────────────────────────────────────────────
    // 필터 / 검색
    // ──────────────────────────────────────────────────────────────────

    private void ChkFilter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!IsLoaded) return;
        var q    = TxtSearch.Text.Trim().ToLower();
        bool all    = ChkAll.IsChecked == true;
        bool ok     = ChkOk.IsChecked  == true;
        bool broken = ChkBroken.IsChecked == true;

        _filtered.Clear();
        foreach (var entry in _all)
        {
            bool statusOk = all ||
                (ok     && entry.Status == ShortcutStatus.Ok) ||
                (broken && entry.Status != ShortcutStatus.Ok);
            if (!statusOk) continue;
            if (!string.IsNullOrEmpty(q) &&
                !entry.Name.ToLower().Contains(q) &&
                !entry.TargetPath.ToLower().Contains(q)) continue;
            _filtered.Add(entry);
        }
        TxtCount.Text = $"{_filtered.Count} / {_all.Count}개";
    }

    // ──────────────────────────────────────────────────────────────────
    // 목록 선택
    // ──────────────────────────────────────────────────────────────────

    private void LstShortcuts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstShortcuts.SelectedItem is ShortcutEntry entry)
        {
            LoadToForm(entry);
            BtnDelete.IsEnabled = true;
        }
        else
        {
            BtnDelete.IsEnabled = false;
        }
    }

    private void LstShortcuts_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LstShortcuts.SelectedItem is ShortcutEntry entry && File.Exists(entry.LnkPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(entry.LnkPath) { UseShellExecute = true });
    }

    // ──────────────────────────────────────────────────────────────────
    // 폼 바인딩
    // ──────────────────────────────────────────────────────────────────

    private void LoadToForm(ShortcutEntry entry)
    {
        _editing = entry;
        _isDirty = false;
        TxtDetailName.Text = entry.Name;
        ImgDetailIcon.Source = entry.IconImage;
        TxtName.Text        = entry.Name;
        TxtSavePath.Text    = Path.GetDirectoryName(entry.LnkPath) ?? "";
        TxtTarget.Text      = entry.TargetPath;
        TxtArguments.Text   = entry.Arguments;
        TxtWorkingDir.Text  = entry.WorkingDir;
        TxtDescription.Text = entry.Description;
        TxtIconPath.Text    = entry.IconPath;
        TxtIconIndex.Text   = entry.IconIndex.ToString();
    }

    private void FormToEntry(ShortcutEntry entry)
    {
        entry.Name       = TxtName.Text.Trim();
        entry.LnkPath    = Path.Combine(TxtSavePath.Text.Trim(),
                                        TxtName.Text.Trim() + ".lnk");
        entry.TargetPath  = TxtTarget.Text.Trim();
        entry.Arguments   = TxtArguments.Text.Trim();
        entry.WorkingDir  = TxtWorkingDir.Text.Trim();
        entry.Description = TxtDescription.Text.Trim();
        entry.IconPath    = TxtIconPath.Text.Trim();
        entry.IconIndex   = int.TryParse(TxtIconIndex.Text, out var i) ? i : 0;
    }

    // ──────────────────────────────────────────────────────────────────
    // CRUD
    // ──────────────────────────────────────────────────────────────────

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var entry = new ShortcutEntry { Name = "새 바로가기" };
        _all.Add(entry);
        ApplyFilter();
        LstShortcuts.SelectedItem = entry;
        TxtName.Focus();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        { MessageBox.Show("이름을 입력하세요.", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(TxtTarget.Text))
        { MessageBox.Show("대상 경로를 입력하세요.", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(TxtSavePath.Text))
        { MessageBox.Show("저장 위치를 입력하세요.", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var entry = _editing ?? new ShortcutEntry();
        FormToEntry(entry);

        try
        {
            Directory.CreateDirectory(TxtSavePath.Text.Trim());
            ShellLinkService.Save(entry);

            // 아이콘 리로드
            entry.IconImage = string.IsNullOrEmpty(entry.IconPath)
                ? IconExtractor.Extract(entry.TargetPath)
                : IconExtractor.Extract(entry.IconPath, entry.IconIndex);

            entry.Status = (!File.Exists(entry.TargetPath) && !Directory.Exists(entry.TargetPath))
                ? ShortcutStatus.BrokenTarget : ShortcutStatus.Ok;

            if (_editing == null) { _all.Add(entry); ApplyFilter(); }
            SetStatus($"저장 완료: {entry.LnkPath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstShortcuts.SelectedItem is not ShortcutEntry entry) return;
        if (MessageBox.Show($"'{entry.Name}' 바로가기를 삭제하시겠습니까?\n({entry.LnkPath})",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        ShellLinkService.Delete(entry.LnkPath);
        _all.Remove(entry);
        ApplyFilter();
        SetStatus($"삭제 완료: {entry.Name}");
    }

    private void BtnFixBroken_Click(object sender, RoutedEventArgs e)
    {
        var broken = _all.Where(x => x.Status != ShortcutStatus.Ok).ToList();
        if (broken.Count == 0) { SetStatus("깨진 링크 없음"); return; }

        if (MessageBox.Show($"깨진 링크 {broken.Count}개를 삭제하시겠습니까?",
            "깨진 링크 정리", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var b in broken)
        {
            ShellLinkService.Delete(b.LnkPath);
            _all.Remove(b);
        }
        ApplyFilter();
        SetStatus($"깨진 링크 {broken.Count}개 삭제");
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        if (_editing != null) LoadToForm(_editing);
    }

    // ──────────────────────────────────────────────────────────────────
    // 일괄 생성
    // ──────────────────────────────────────────────────────────────────

    private void BtnBatchCreate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new BatchCreateDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        int created = 0, failed = 0;
        foreach (var entry in dlg.Entries)
        {
            try
            {
                ShellLinkService.Create(entry);
                entry.IconImage = IconExtractor.Extract(entry.TargetPath);
                _all.Add(entry);
                created++;
            }
            catch { failed++; }
        }
        ApplyFilter();
        SetStatus($"일괄 생성 — 성공 {created}개, 실패 {failed}개");
    }

    // ──────────────────────────────────────────────────────────────────
    // 파일 탐색기 버튼들
    // ──────────────────────────────────────────────────────────────────

    private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Title = "대상 파일 선택", Filter = "모든 파일|*.*|실행 파일|*.exe" };
        if (ofd.ShowDialog(this) == true) TxtTarget.Text = ofd.FileName;
    }

    private void BtnBrowseSavePath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "바로가기 저장 위치 선택" };
        if (dlg.ShowDialog(this) == true) TxtSavePath.Text = dlg.FolderName;
    }

    private void BtnBrowseWorkDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "시작 위치 선택" };
        if (dlg.ShowDialog(this) == true) TxtWorkingDir.Text = dlg.FolderName;
    }

    private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Title = "아이콘 파일 선택", Filter = "아이콘/실행파일|*.ico;*.exe;*.dll|모든 파일|*.*" };
        if (ofd.ShowDialog(this) == true) TxtIconPath.Text = ofd.FileName;
    }

    // ──────────────────────────────────────────────────────────────────
    // 빠른 위치 선택
    // ──────────────────────────────────────────────────────────────────

    private void PopulateQuickLocations()
    {
        CmbQuickLocation.Items.Clear();
        CmbQuickLocation.Items.Add(new ComboBoxItem { Content = "— 빠른 위치 선택 —", IsEnabled = false });
        foreach (var f in ShortcutScanner.WellKnownFolders())
            CmbQuickLocation.Items.Add(new ComboBoxItem { Content = f, Tag = f });
        CmbQuickLocation.SelectedIndex = 0;
    }

    private void CmbQuickLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbQuickLocation.SelectedItem is ComboBoxItem { Tag: string path })
            TxtSavePath.Text = path;
    }

    // ──────────────────────────────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────────────────────────────

    private void SetStatus(string msg) => TxtStatus.Text = msg;
}

// ══════════════════════════════════════════════════════════════════════
// 일괄 생성 다이얼로그
// ══════════════════════════════════════════════════════════════════════

public partial class BatchCreateDialog : Window
{
    public List<ShortcutEntry> Entries { get; } = [];

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public BatchCreateDialog()
    {
        Title = "일괄 바로가기 생성";
        Width = 640; Height = 480;
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        // 설명
        var hdr = new TextBlock
        {
            Text = "대상 파일 경로를 한 줄에 하나씩 입력하세요.\n저장 위치를 선택하면 각 파일명으로 바로가기를 일괄 생성합니다.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 11, Margin = new Thickness(12, 10, 12, 6), TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        var txt = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap,
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Margin = new Thickness(12, 0, 12, 0), Padding = new Thickness(6)
        };
        var sv = new ScrollViewer { Content = txt, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12, 0, 12, 8) };
        Grid.SetRow(sv, 1); root.Children.Add(sv);

        // 저장 위치 행
        var locRow = new Grid { Margin = new Thickness(12, 0, 12, 8) };
        locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var locLabel = new TextBlock { Text = "저장 위치:", Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var locBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Padding = new Thickness(6, 4, 6, 4) };
        locBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var browseBtn = new Button { Content = "…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4, 0, 0, 0) };
        browseBtn.Click += (_, _) =>
        {
            var d = new OpenFolderDialog { Title = "저장 위치 선택" };
            if (d.ShowDialog(this) == true) locBox.Text = d.FolderName;
        };
        Grid.SetColumn(locLabel, 0); locRow.Children.Add(locLabel);
        Grid.SetColumn(locBox, 1);   locRow.Children.Add(locBox);
        Grid.SetColumn(browseBtn, 2); locRow.Children.Add(browseBtn);
        Grid.SetRow(locRow, 2); root.Children.Add(locRow);

        // 버튼 행
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 12, 12) };
        var cancel = new Button { Content = "취소", Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(0, 0, 6, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "생성", Padding = new Thickness(16, 5, 16, 5), Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1A, 0x0A)) };
        ok.Click += (_, _) =>
        {
            Entries.Clear();
            foreach (var line in txt.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!File.Exists(line) && !Directory.Exists(line)) continue;
                Entries.Add(new ShortcutEntry
                {
                    Name = Path.GetFileNameWithoutExtension(line),
                    LnkPath = Path.Combine(locBox.Text, Path.GetFileNameWithoutExtension(line) + ".lnk"),
                    TargetPath = line,
                    WorkingDir = Path.GetDirectoryName(line) ?? ""
                });
            }
            if (Entries.Count == 0) { MessageBox.Show("유효한 경로가 없습니다.", "오류"); return; }
            DialogResult = true; Close();
        };
        btns.Children.Add(cancel);
        btns.Children.Add(ok);
        Grid.SetRow(btns, 3); root.Children.Add(btns);

        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
        };
    }
}
