using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DiskLens.Models;
using DiskLens.Services;
using Microsoft.Win32;

namespace DiskLens;

// ── 컨버터: % → 크기 바 너비 ──────────────────────────────────────────
public class PercentToWidthConverter : System.Windows.Data.IValueConverter
{
    public double ColumnWidth { get; set; } = 150;
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double pct ? Math.Max(0, Math.Min(ColumnWidth, pct / 100.0 * ColumnWidth)) : 0.0;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// ── 컨버터: bool → 브러시 ─────────────────────────────────────────────
public class BoolToBrushConverter : System.Windows.Data.IValueConverter
{
    public Brush? TrueValue { get; set; }
    public Brush? FalseValue { get; set; }
    public object? Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? TrueValue : FalseValue;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// ── 브레드크럼 아이템 ─────────────────────────────────────────────────
record BreadcrumbItem(string Name, DiskItem? Item);

public partial class MainWindow : Window
{
    // ── 설정 경로 ────────────────────────────────────────────────────
    private static readonly string SettingsFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiskLens", "settings.txt");

    // ── 상태 ─────────────────────────────────────────────────────────
    private DiskItem? _root;
    private DiskItem? _currentRoot;
    private readonly ObservableCollection<BreadcrumbItem> _breadcrumbs = [];
    private CancellationTokenSource? _cts;
    private readonly DiskScanner _scanner = new();
    private string _sortColumn = "Size";
    private bool _sortAscending = false;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _watcherDebounce;
    private string _currentView = "Tree";

    // 트리맵용
    private DiskItem? _mapRoot;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadDrives();
        Breadcrumb.ItemsSource = _breadcrumbs;
        SetViewMode("Tree");
        RestoreLastPath();
    }

    private void RestoreLastPath()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var saved = File.ReadAllText(SettingsFile).Trim();
                if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
                    TxtPath.Text = saved;
            }
        }
        catch { }
    }

    private void SaveLastPath(string path)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, path);
        }
        catch { }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5) BtnRefresh_Click(this, new RoutedEventArgs());
        if (e.Key == Key.Back && _breadcrumbs.Count > 1) NavigateToBreadcrumb(_breadcrumbs.Count - 2);
    }

    // ═════════════════════════════════════════════════════════════════
    // 드라이브 로드
    // ═════════════════════════════════════════════════════════════════
    private void LoadDrives()
    {
        CbDrive.Items.Clear();
        foreach (var di in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = string.IsNullOrEmpty(di.VolumeLabel) ? di.Name : $"{di.VolumeLabel} ({di.Name})";
            var free = DiskItem.FormatSize(di.AvailableFreeSpace);
            var total = DiskItem.FormatSize(di.TotalSize);
            CbDrive.Items.Add(new ComboBoxItem
            {
                Content = $"{label}  [{free} 여유 / {total}]",
                Tag = di.RootDirectory.FullName,
            });
        }
        if (CbDrive.Items.Count > 0) CbDrive.SelectedIndex = 0;
    }

    private void CbDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CbDrive.SelectedItem is ComboBoxItem ci && ci.Tag is string path)
            TxtPath.Text = path;
    }

    // ═════════════════════════════════════════════════════════════════
    // 스캔
    // ═════════════════════════════════════════════════════════════════
    private async void BtnScan_Click(object sender, RoutedEventArgs e) => await StartScanAsync(TxtPath.Text.Trim());

    private void TxtPath_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = StartScanAsync(TxtPath.Text.Trim());
    }

    private async Task StartScanAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show("유효한 경로를 입력하세요.", "Disk.Lens", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetScanningState(true);
        StopWatcher();
        MainTree.Items.Clear();
        _root = null;
        _currentRoot = null;
        _breadcrumbs.Clear();
        UpdateStatusBar(null, path);

        var progress = new Progress<ScanProgress>(p =>
        {
            TbScanStatus.Text = $"스캔 중: {p.ItemsScanned:N0}개 항목  {DiskItem.FormatSize(p.TotalSize)}";
        });

        try
        {
            var sw = Stopwatch.StartNew();
            _root = await _scanner.ScanAsync(path, progress, _cts.Token);
            sw.Stop();

            _currentRoot = _root;
            UpdateBreadcrumbs(_root);
            ApplySortAndDisplay(_root);
            UpdateStatusBar(_root, path);
            SaveLastPath(path);

            TbScanStatus.Text = $"완료: {_root.FileCount:N0}개 파일, {_root.FolderCount:N0}개 폴더  ({sw.Elapsed.TotalSeconds:F1}초)";

            StartWatcher(path);
            RefreshSecondaryViews();
        }
        catch (OperationCanceledException)
        {
            TbScanStatus.Text = "스캔 취소됨";
        }
        catch (Exception ex)
        {
            TbScanStatus.Text = $"오류: {ex.Message}";
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtPath.Text))
            _ = StartScanAsync(TxtPath.Text.Trim());
    }

    private void SetScanningState(bool scanning)
    {
        BtnScan.IsEnabled = !scanning;
        BtnCancel.IsEnabled = scanning;
        PbScan.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        if (!scanning) TbScanStatus.Text = TbScanStatus.Text; // keep last message
    }

    // ═════════════════════════════════════════════════════════════════
    // 트리 표시 & 정렬
    // ═════════════════════════════════════════════════════════════════
    private void ApplySortAndDisplay(DiskItem item)
    {
        var sorted = SortChildren(item.Children.ToList());
        MainTree.Items.Clear();
        foreach (var c in sorted)
            MainTree.Items.Add(c);
    }

    private List<DiskItem> SortChildren(List<DiskItem> items)
    {
        IOrderedEnumerable<DiskItem> q = _sortColumn switch
        {
            "Name"     => _sortAscending ? items.OrderBy(i => i.Name) : items.OrderByDescending(i => i.Name),
            "Size"     => _sortAscending ? items.OrderBy(i => i.Size) : items.OrderByDescending(i => i.Size),
            "Alloc"    => _sortAscending ? items.OrderBy(i => i.AllocatedSize) : items.OrderByDescending(i => i.AllocatedSize),
            "Pct"      => _sortAscending ? items.OrderBy(i => i.PercentOfParent) : items.OrderByDescending(i => i.PercentOfParent),
            "Files"    => _sortAscending ? items.OrderBy(i => i.FileCount) : items.OrderByDescending(i => i.FileCount),
            "Modified" => _sortAscending ? items.OrderBy(i => i.LastModified) : items.OrderByDescending(i => i.LastModified),
            _ => items.OrderByDescending(i => i.Size),
        };
        // 폴더 우선 (정렬 순서 유지)
        var dirs = q.Where(i => i.IsDirectory);
        var files = q.Where(i => !i.IsDirectory);
        return [.. dirs.Concat(files)];
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string col)
        {
            if (_sortColumn == col) _sortAscending = !_sortAscending;
            else { _sortColumn = col; _sortAscending = false; }

            if (_currentRoot != null) ApplySortAndDisplay(_currentRoot);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 네비게이션
    // ═════════════════════════════════════════════════════════════════
    private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MainTree.SelectedItem is DiskItem item && item.IsDirectory && !item.AccessDenied)
            DrillDown(item);
    }

    private void DrillDown(DiskItem item)
    {
        _currentRoot = item;
        _breadcrumbs.Add(new BreadcrumbItem(item.Name, item));
        ApplySortAndDisplay(item);
        UpdateStatusBar(item, item.FullPath);
        if (_currentView == "Map") DrawTreeMap(item);
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BreadcrumbItem bc)
        {
            var idx = _breadcrumbs.IndexOf(bc);
            if (idx >= 0) NavigateToBreadcrumb(idx);
        }
    }

    private void NavigateToBreadcrumb(int idx)
    {
        while (_breadcrumbs.Count > idx + 1)
            _breadcrumbs.RemoveAt(_breadcrumbs.Count - 1);

        var item = _breadcrumbs[idx].Item;
        _currentRoot = item;
        if (item != null) ApplySortAndDisplay(item);
        else if (_root != null) ApplySortAndDisplay(_root);
    }

    private void UpdateBreadcrumbs(DiskItem root)
    {
        _breadcrumbs.Clear();
        _breadcrumbs.Add(new BreadcrumbItem(root.Name.Length > 0 ? root.Name : root.FullPath, root));
    }

    // ═════════════════════════════════════════════════════════════════
    // 뷰 모드 전환
    // ═════════════════════════════════════════════════════════════════
    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
            SetViewMode(mode);
    }

    private void SetViewMode(string mode)
    {
        _currentView = mode;
        PanelTree.Visibility = mode == "Tree" ? Visibility.Visible : Visibility.Collapsed;
        PanelMap.Visibility  = mode == "Map"  ? Visibility.Visible : Visibility.Collapsed;
        PanelTop.Visibility  = mode == "Top"  ? Visibility.Visible : Visibility.Collapsed;
        PanelExt.Visibility  = mode == "Ext"  ? Visibility.Visible : Visibility.Collapsed;

        // 활성 버튼 강조
        foreach (var btn in new[] { BtnViewTree, BtnViewMap, BtnViewTop, BtnViewExt })
        {
            btn.Background = (string)btn.Tag == mode
                ? new SolidColorBrush(Color.FromRgb(30, 74, 106))
                : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        }

        if (mode == "Map" && _currentRoot != null) DrawTreeMap(_currentRoot);
        if (mode == "Top" && _root != null) LoadTopFiles(_root);
        if (mode == "Ext" && _root != null) LoadExtensions(_root);
    }

    private void RefreshSecondaryViews()
    {
        if (_currentView == "Map" && _currentRoot != null) DrawTreeMap(_currentRoot);
        if (_currentView == "Top" && _root != null) LoadTopFiles(_root);
        if (_currentView == "Ext" && _root != null) LoadExtensions(_root);
    }

    // ═════════════════════════════════════════════════════════════════
    // 상위 파일
    // ═════════════════════════════════════════════════════════════════
    private void LoadTopFiles(DiskItem root)
    {
        var files = DiskScanner.GetTopFiles(root, 200);
        TopFilesList.ItemsSource = files;
    }

    private void TopFiles_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TopFilesList.SelectedItem is DiskItem item)
            OpenInExplorer(item.FullPath);
    }

    private void TopMnuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (TopFilesList.SelectedItem is DiskItem item) OpenInExplorer(item.FullPath);
    }

    private void TopMnuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (TopFilesList.SelectedItem is DiskItem item) Clipboard.SetText(item.FullPath);
    }

    private void TopMnuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (TopFilesList.SelectedItem is DiskItem item) DeleteItem(item);
    }

    // ═════════════════════════════════════════════════════════════════
    // 확장자 분석
    // ═════════════════════════════════════════════════════════════════
    private void LoadExtensions(DiskItem root)
    {
        var exts = DiskScanner.GetExtensionStats(root);
        long maxSize = exts.Count > 0 ? exts[0].TotalSize : 1;

        ExtList.ItemsSource = exts;

        // 크기 바 너비 후처리 (DataTemplate에서 직접 바인딩이 어려우므로)
        ExtList.Loaded -= ExtList_AfterLoad;
        ExtList.Loaded += ExtList_AfterLoad;
        void ExtList_AfterLoad(object s, RoutedEventArgs ev)
        {
            ExtList.Loaded -= ExtList_AfterLoad;
            UpdateExtBars(exts, maxSize);
        }
        UpdateExtBars(exts, maxSize);
    }

    private void UpdateExtBars(List<ExtensionInfo> exts, long maxSize)
    {
        // 실제 너비는 코드로 처리 (XAML에서 복잡한 바인딩 대신)
        // ListView 아이템 렌더링 후 업데이트 - Dispatcher로 지연
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            foreach (ListViewItem? lvi in ExtList.Items.OfType<ExtensionInfo>()
                .Select(e => ExtList.ItemContainerGenerator.ContainerFromItem(e) as ListViewItem)
                .Where(l => l != null))
            {
                if (lvi?.DataContext is ExtensionInfo info)
                {
                    var bar = FindChild<Border>(lvi, "Bar");
                    if (bar != null)
                        bar.Width = Math.Max(2, info.TotalSize * 185.0 / maxSize);
                }
            }
        });
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == name) return t;
            var found = FindChild<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ═════════════════════════════════════════════════════════════════
    // 트리맵
    // ═════════════════════════════════════════════════════════════════
    private void DrawTreeMap(DiskItem root)
    {
        _mapRoot = root;
        TbMapHint.Visibility = Visibility.Collapsed;
        TreeMapCanvas.Children.Clear();
        TreeMapCanvas.Children.Add(TbMapHint);

        if (root.Size == 0) { TbMapHint.Visibility = Visibility.Visible; return; }

        double w = TreeMapCanvas.ActualWidth;
        double h = TreeMapCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        var items = root.Children.Where(c => c.Size > 0).OrderByDescending(c => c.Size).ToList();
        RenderTreeMap(items, 0, 0, w, h, root.Size, 0);
    }

    private static readonly string[] MapColors =
    [
        "#1565C0", "#1976D2", "#1E88E5", "#42A5F5",
        "#2E7D32", "#388E3C", "#43A047", "#66BB6A",
        "#6A1B9A", "#7B1FA2", "#8E24AA", "#AB47BC",
        "#E65100", "#F57C00", "#FB8C00", "#FFA726",
        "#B71C1C", "#C62828", "#D32F2F", "#EF5350",
        "#004D40", "#00695C", "#00796B", "#26A69A",
    ];

    // 파일용: 폴더 팔레트와 동일한 색조, 채도만 낮춤
    private static Color DesaturateForFile(Color c)
    {
        double gray = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        const double factor = 0.30; // 원색 비율 (낮을수록 더 회색)
        return Color.FromRgb(
            (byte)(c.R * factor + gray * (1 - factor)),
            (byte)(c.G * factor + gray * (1 - factor)),
            (byte)(c.B * factor + gray * (1 - factor)));
    }

    private void RenderTreeMap(List<DiskItem> items, double x, double y, double w, double h, long totalSize, int colorOffset)
    {
        if (items.Count == 0 || w < 4 || h < 4) return;

        // Squarified treemap 알고리즘
        bool horizontal = w >= h;
        var arranged = new List<(DiskItem item, Rect rect)>();

        double pos = horizontal ? x : y;
        var row = new List<DiskItem>();
        double rowSize = 0;
        double shortSide = horizontal ? h : w;

        void FlushRow()
        {
            if (row.Count == 0) return;
            double step = horizontal ? y : x;
            foreach (var ri in row)
            {
                double thick = rowSize > 0 ? (horizontal
                    ? (w * (double)rowSize / totalSize)
                    : (h * (double)rowSize / totalSize)) : 0;

                double itemLen = shortSide * (double)ri.Size / rowSize;
                Rect rect = horizontal
                    ? new(pos, step, thick, itemLen)
                    : new(step, pos, itemLen, thick);
                arranged.Add((ri, rect));
                step += itemLen;
            }
            pos += horizontal ? (w * (double)rowSize / totalSize) : (h * (double)rowSize / totalSize);
            row.Clear();
            rowSize = 0;
        }

        foreach (var item in items)
        {
            row.Add(item);
            rowSize += item.Size;
        }
        FlushRow();

        int ci = colorOffset;
        foreach (var (item, rect) in arranged)
        {
            if (rect.Width < 2 || rect.Height < 2) continue;

            var baseColor = (Color)ColorConverter.ConvertFromString(MapColors[ci % MapColors.Length]);
            ci++;

            // 폴더: 비비드 컬러 / 파일: 채도 낮은 회색 계열
            var color = item.IsDirectory ? baseColor : DesaturateForFile(baseColor);
            double opacity = item.IsDirectory ? 0.85 : 0.75;

            var border = new Border
            {
                Width = rect.Width - 1,
                Height = rect.Height - 1,
                Background = new SolidColorBrush(color) { Opacity = opacity },
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(1),
                ToolTip = $"{item.Name}\n{item.SizeText}  ({item.PercentText})",
                Tag = item,
                Cursor = Cursors.Hand,
            };

            if (rect.Width > 40 && rect.Height > 20)
            {
                // 파일은 텍스트를 약간 흐리게 처리해 폴더와 명확히 구분
                var textColor = item.IsDirectory
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(200, 220, 220, 220));

                var tb = new TextBlock
                {
                    Text = item.Name,
                    Foreground = textColor,
                    FontSize = Math.Max(9, Math.Min(13, rect.Width / 10)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(3, 2, 3, 0),
                    IsHitTestVisible = false,
                };
                border.Child = tb;
            }

            Canvas.SetLeft(border, rect.X);
            Canvas.SetTop(border, rect.Y);
            TreeMapCanvas.Children.Add(border);
        }
    }

    private void TreeMap_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && fe.Tag is DiskItem item)
        {
            if (e.LeftButton == MouseButtonState.Pressed && item.IsDirectory)
            {
                DrillDown(item);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                // 상위 이동
                if (_breadcrumbs.Count > 1)
                    NavigateToBreadcrumb(_breadcrumbs.Count - 2);
            }
        }
        else if (e.RightButton == MouseButtonState.Pressed && _breadcrumbs.Count > 1)
        {
            NavigateToBreadcrumb(_breadcrumbs.Count - 2);
        }
    }

    private void TreeMapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_currentRoot != null && _currentView == "Map")
            DrawTreeMap(_currentRoot);
    }

    // ═════════════════════════════════════════════════════════════════
    // 컨텍스트 메뉴
    // ═════════════════════════════════════════════════════════════════
    private DiskItem? SelectedTreeItem => MainTree.SelectedItem as DiskItem;

    private void MnuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTreeItem is { } item) OpenInExplorer(item.FullPath);
    }

    private void MnuDrillDown_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTreeItem is { IsDirectory: true } item) DrillDown(item);
    }

    private void MnuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTreeItem is { } item) Clipboard.SetText(item.FullPath);
    }

    private void MnuCopySize_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTreeItem is { } item) Clipboard.SetText(item.SizeText);
    }

    private void MnuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTreeItem is { } item) DeleteItem(item);
    }

    // ═════════════════════════════════════════════════════════════════
    // 파일 작업
    // ═════════════════════════════════════════════════════════════════
    private static void OpenInExplorer(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start("explorer.exe", path);
            else if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch { }
    }

    private void DeleteItem(DiskItem item)
    {
        var msg = item.IsDirectory
            ? $"폴더를 삭제하시겠습니까?\n\n{item.FullPath}\n\n크기: {item.SizeText}"
            : $"파일을 삭제하시겠습니까?\n\n{item.FullPath}\n\n크기: {item.SizeText}";

        var result = MessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (item.IsDirectory)
                Directory.Delete(item.FullPath, recursive: true);
            else
                File.Delete(item.FullPath);

            // 트리에서 제거 및 부모 통계 갱신
            RemoveFromParent(item);
            if (_root != null) RecalcStats(_root);
            UpdateStatusBar(_currentRoot ?? _root, TxtPath.Text);
            TbScanStatus.Text = $"삭제 완료: {item.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"삭제 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MainTree_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not TreeViewItem)
            source = VisualTreeHelper.GetParent(source);
        if (source is TreeViewItem tvi)
            tvi.IsSelected = true;
    }

    private void RemoveFromParent(DiskItem item)
    {
        MainTree.Items.Remove(item);
        if (_root != null) RemoveChildRecursive(_root, item);
    }

    private static bool RemoveChildRecursive(DiskItem parent, DiskItem target)
    {
        if (parent.Children.Remove(target)) return true;
        foreach (var child in parent.Children)
            if (RemoveChildRecursive(child, target)) return true;
        return false;
    }

    // 삭제 후 트리 전체 통계 재계산
    private static void RecalcStats(DiskItem item)
    {
        if (!item.IsDirectory) return;
        long size = 0, alloc = 0, files = 0, folders = 0;
        foreach (var child in item.Children)
        {
            RecalcStats(child);
            size += child.Size;
            alloc += child.AllocatedSize;
            files += child.IsDirectory ? child.FileCount : 1;
            folders += child.IsDirectory ? child.FolderCount + 1 : 0;
        }
        item.Size = size;
        item.AllocatedSize = alloc;
        item.FileCount = files;
        item.FolderCount = folders;
        if (size > 0)
            foreach (var child in item.Children)
                child.PercentOfParent = child.Size * 100.0 / size;
    }

    // ═════════════════════════════════════════════════════════════════
    // CSV 내보내기
    // ═════════════════════════════════════════════════════════════════
    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_root == null) { MessageBox.Show("먼저 스캔을 실행하세요.", "Disk.Lens"); return; }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV 파일|*.csv",
            FileName = $"DiskLens_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("경로,종류,크기(bytes),크기,파일수,폴더수,수정일");
            ExportCsvRecursive(_root, sb, 0);
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            TbScanStatus.Text = $"CSV 내보내기 완료: {dlg.FileName}";
            Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"내보내기 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ExportCsvRecursive(DiskItem item, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"\"{indent}{item.FullPath}\",{(item.IsDirectory ? "폴더" : "파일")},{item.Size},{item.SizeText},{item.FileCount},{item.FolderCount},{item.LastModifiedText}");
        if (item.IsDirectory)
            foreach (var child in item.Children.OrderByDescending(c => c.Size))
                ExportCsvRecursive(child, sb, depth + 1);
    }

    // ═════════════════════════════════════════════════════════════════
    // 상태바
    // ═════════════════════════════════════════════════════════════════
    private void UpdateStatusBar(DiskItem? item, string path)
    {
        try
        {
            var drivePath = System.IO.Path.GetPathRoot(path) ?? path;
            var di = new DriveInfo(drivePath);
            StDrive.Text = $"{di.Name}  ({di.DriveType})";
            StTotal.Text = DiskItem.FormatSize(di.TotalSize);
            StFree.Text = DiskItem.FormatSize(di.AvailableFreeSpace);
        }
        catch
        {
            StDrive.Text = path;
        }

        if (item != null)
        {
            StFiles.Text = item.FileCount.ToString("N0");
            StFolders.Text = item.FolderCount.ToString("N0");
        }
        else
        {
            StFiles.Text = "-";
            StFolders.Text = "-";
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // FileSystemWatcher (자동 갱신)
    // ═════════════════════════════════════════════════════════════════
    private void StartWatcher(string path)
    {
        StopWatcher();
        try
        {
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileSystemChanged;
            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
        }
        catch { /* 접근 거부 등 무시 */ }
    }

    private void StopWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        _watcherDebounce?.Stop();
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_watcherDebounce == null)
            {
                _watcherDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _watcherDebounce.Tick += (s, ev) =>
                {
                    _watcherDebounce.Stop();
                    if (!string.IsNullOrEmpty(TxtPath.Text))
                        TbScanStatus.Text = "파일 변경 감지됨. F5로 새로고침";
                };
            }
            _watcherDebounce.Stop();
            _watcherDebounce.Start();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        StopWatcher();
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
