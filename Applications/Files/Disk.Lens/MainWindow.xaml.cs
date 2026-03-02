using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DiskLens;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 상태 ─────────────────────────────────────────────────────────────────
    private FileNode?  _root;
    private FileNode?  _currentNode;
    private CancellationTokenSource? _cts;
    private readonly Stack<FileNode> _navStack = new();
    private FileNode? _rightClickedNode;

    private readonly ObservableCollection<BreadcrumbItem> _breadcrumb = [];
    private readonly ObservableCollection<TopFileEntry>   _topFiles   = [];
    private readonly ObservableCollection<LegendItem>     _legend     = [];

    // ── 초기화 ────────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        Breadcrumb.ItemsSource = _breadcrumb;
        LstTop20.ItemsSource   = _topFiles;
        Legend.ItemsSource     = _legend;

        LoadDrives();
        LoadLegend();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int val  = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    private void LoadDrives()
    {
        var items = new List<DriveItem>();

        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            string label = $"{d.Name.TrimEnd('\\')}  ({d.VolumeLabel})  {FileNode.FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {FileNode.FormatSize(d.TotalSize)}";
            items.Add(new DriveItem { Path = d.RootDirectory.FullName, Display = label });
        }

        CboDrives.ItemsSource = items;
        if (items.Count > 0) CboDrives.SelectedIndex = 0;
    }

    private void LoadLegend()
    {
        foreach (var (color, label) in ExtensionColors.Categories)
            _legend.Add(new LegendItem { Color = color, Label = label });
    }

    // ── 스캔 ──────────────────────────────────────────────────────────────────
    private async void BtnScan_Click(object sender, RoutedEventArgs e) => await StartScanAsync();
    private void      BtnCancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private string? GetSelectedPath()
    {
        if (CboDrives.SelectedItem is DriveItem d) return d.Path;
        return null;
    }

    private async Task StartScanAsync()
    {
        var path = GetSelectedPath();
        if (path is null) return;

        // UI 초기화
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetScanningState(true);

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                TxtProgress.Text   = $"{p.ScannedCount:N0}개 처리 중...";
                TxtScanDetail.Text = p.CurrentPath;
            });

            _root = await ScanService.ScanAsync(path, progress, _cts.Token);

            if (_root != null)
            {
                _currentNode = _root;
                _navStack.Clear();
                ShowNode(_root);
                TxtStatus.Text = $"스캔 완료: {path}";
            }
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "스캔 취소됨";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"오류: {ex.Message}";
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private void SetScanningState(bool scanning)
    {
        BtnScan.Visibility    = scanning ? Visibility.Collapsed : Visibility.Visible;
        BtnCancel.Visibility  = scanning ? Visibility.Visible   : Visibility.Collapsed;
        PnlProgress.Visibility = scanning ? Visibility.Visible  : Visibility.Collapsed;
        PnlScanning.Visibility  = scanning ? Visibility.Visible : Visibility.Collapsed;
        CboDrives.IsEnabled   = !scanning;
    }

    private void ShowNode(FileNode node)
    {
        _currentNode = node;

        // 트리맵 표시
        PnlEmpty.Visibility  = Visibility.Collapsed;
        Treemap.Visibility   = Visibility.Visible;
        Treemap.RootNode     = node;

        // 통계
        var (fileCount, dirCount) = CountStats(node);
        TxtFileCount.Text = $"파일 {fileCount:N0}개";
        TxtDirCount.Text  = $"폴더 {dirCount:N0}개";
        TxtTotalSize.Text = node.SizeText;

        // TOP 20
        _topFiles.Clear();
        foreach (var f in ScanService.GetTopFiles(node))
            _topFiles.Add(f);

        // 브레드크럼
        UpdateBreadcrumb(node);

        // 위로 버튼
        BtnUp.IsEnabled = node.Parent != null || _navStack.Count > 0;
    }

    private void UpdateBreadcrumb(FileNode node)
    {
        _breadcrumb.Clear();
        var chain = new Stack<FileNode>();
        var cur   = node;
        while (cur != null) { chain.Push(cur); cur = cur.Parent; }
        while (chain.Count > 0)
        {
            var n = chain.Pop();
            _breadcrumb.Add(new BreadcrumbItem { Name = n.Name, Node = n });
        }
    }

    private static (int files, int dirs) CountStats(FileNode node)
    {
        int f = 0, d = 0;
        CountRec(node, ref f, ref d);
        return (f, d);
    }

    private static void CountRec(FileNode node, ref int files, ref int dirs)
    {
        foreach (var c in node.Children)
        {
            if (c.IsDirectory) { dirs++; CountRec(c, ref files, ref dirs); }
            else files++;
        }
    }

    // ── 드릴다운 / 네비게이션 ─────────────────────────────────────────────────
    private void Treemap_NodeClicked(FileNode node)
    {
        if (!node.IsDirectory) return;
        _navStack.Push(_currentNode!);
        ShowNode(node);
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentNode?.Parent != null)
        {
            ShowNode(_currentNode.Parent);
        }
        else if (_navStack.Count > 0)
        {
            ShowNode(_navStack.Pop());
        }
    }

    private void BreadcrumbItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is FileNode node)
        {
            _navStack.Clear();
            ShowNode(node);
        }
    }

    // ── 드라이브 선택 ─────────────────────────────────────────────────────────
    private void CboDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // 선택만 바뀜 — 자동 스캔 없음
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "스캔할 폴더 선택" };
        if (dlg.ShowDialog(this) == true)
        {
            var selected = dlg.FolderName;
            var items    = CboDrives.ItemsSource as List<DriveItem> ?? [];
            var existing = items.FirstOrDefault(d => d.Path == selected);
            if (existing == null)
            {
                var newList = items.ToList();
                newList.Insert(0, new DriveItem { Path = selected, Display = selected });
                CboDrives.ItemsSource   = newList;
                CboDrives.SelectedIndex = 0;
            }
            else
            {
                CboDrives.SelectedItem = existing;
            }
        }
    }

    // ── TOP 20 선택 ───────────────────────────────────────────────────────────
    private void LstTop20_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (LstTop20.SelectedItem is TopFileEntry entry)
            Treemap.Highlight(entry.FullPath);
        else
            Treemap.Highlight(null);
    }

    // ── 우클릭 컨텍스트 메뉴 ──────────────────────────────────────────────────
    private void Treemap_NodeRightClicked(FileNode node)
    {
        _rightClickedNode = node;
        MenuDelete.IsEnabled = !node.IsDirectory || node.Children.Count == 0;

        var menu = new System.Windows.Controls.ContextMenu
        {
            Background    = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            BorderBrush   = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            BorderThickness = new Thickness(1),
            Foreground    = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
        };

        var miOpen  = MakeMenuItem("탐색기에서 열기", MenuOpenExplorer_Click);
        var miCopy  = MakeMenuItem("경로 복사", MenuCopyPath_Click);
        var miSep   = new Separator { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) };
        var miDel   = MakeMenuItem("삭제", MenuDelete_Click);
        miDel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

        menu.Items.Add(miOpen);
        menu.Items.Add(miCopy);
        menu.Items.Add(miSep);
        menu.Items.Add(miDel);
        menu.IsOpen = true;
    }

    private static System.Windows.Controls.MenuItem MakeMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new System.Windows.Controls.MenuItem
        {
            Header     = header,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
        };
        item.Click += handler;
        return item;
    }

    private void MenuOpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedNode is null) return;
        try
        {
            if (_rightClickedNode.IsDirectory)
                Process.Start("explorer.exe", _rightClickedNode.FullPath);
            else
                Process.Start("explorer.exe", $"/select,\"{_rightClickedNode.FullPath}\"");
        }
        catch { }
    }

    private void MenuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedNode is null) return;
        try { System.Windows.Clipboard.SetText(_rightClickedNode.FullPath); }
        catch { }
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedNode is null) return;
        var name = _rightClickedNode.Name;
        var msg  = _rightClickedNode.IsDirectory
            ? $"폴더 '{name}' 를 삭제하시겠습니까?\n(하위 파일 포함)"
            : $"파일 '{name}' 를 삭제하시겠습니까?";

        if (System.Windows.MessageBox.Show(msg, "삭제 확인",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            if (_rightClickedNode.IsDirectory)
                Directory.Delete(_rightClickedNode.FullPath, recursive: true);
            else
                File.Delete(_rightClickedNode.FullPath);

            // 트리에서 제거
            _rightClickedNode.Parent?.Children.Remove(_rightClickedNode);
            if (_currentNode != null) ShowNode(_currentNode);
            TxtStatus.Text = $"삭제 완료: {_rightClickedNode.FullPath}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"삭제 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 헬퍼 타입 ─────────────────────────────────────────────────────────────
    private record DriveItem { public string Path { get; init; } = ""; public string Display { get; init; } = ""; }
    private record BreadcrumbItem { public string Name { get; init; } = ""; public FileNode Node { get; init; } = null!; }
    public  record LegendItem    { public Color  Color { get; init; }     public string Label { get; init; } = ""; }
}
