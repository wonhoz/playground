using Microsoft.Win32;

namespace DeepDiff.Views;

public partial class FolderCompareView : UserControl
{
    private readonly MainWindow _main;
    private readonly FolderDiffService _svc = new();
    private readonly FileOperationService _fops = new();

    private List<FolderDiffItem> _allItems = [];
    private string _filterMode = "All";
    private string? _currentLeftPath;
    private string? _currentRightPath;

    private GridViewColumnHeader? _sortHeader;
    private string? _sortColumn;
    private bool _sortAsc = true;

    public FolderCompareView(MainWindow main, string? leftPath = null, string? rightPath = null)
    {
        _main = main;
        InitializeComponent();

        if (leftPath  != null) TxtLeftPath.Text  = leftPath;
        if (rightPath != null) TxtRightPath.Text = rightPath;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(TxtLeftPath.Text) && !string.IsNullOrEmpty(TxtRightPath.Text))
                RunCompare();
        };
    }

    // ─── 비교 실행 ─────────────────────────────────────────────

    private async void RunCompare()
    {
        string left  = TxtLeftPath.Text.Trim();
        string right = TxtRightPath.Text.Trim();
        if (!Directory.Exists(left) || !Directory.Exists(right))
        {
            TbStatus.Text = "유효한 폴더 경로를 입력하세요.";
            return;
        }

        _currentLeftPath  = left;
        _currentRightPath = right;
        TbStatus.Text = "비교 중...";

        bool recurse = ChkRecurse.IsChecked == true;
        string filter = TxtFilter.Text.Trim().Length > 0 ? TxtFilter.Text.Trim() : "*.*";

        var result = await Task.Run(() => _svc.Compare(left, right, filter, recurse));
        _allItems = result.Items;

        UpdateStats(result);
        ApplyFilterAndSearch();
    }

    private void UpdateStats(FolderDiffService.FolderDiffResult r)
    {
        TbSame.Text      = $"동일 {r.SameCount}";
        TbDiff.Text      = $"차이 {r.DiffCount}";
        TbLeftOnly.Text  = $"좌만 {r.LeftOnlyCount}";
        TbRightOnly.Text = $"우만 {r.RightOnlyCount}";
    }

    private void ApplyFilterAndSearch()
    {
        if (!IsLoaded) return;
        var search = TxtSearch.Text.Trim().ToLower();

        IEnumerable<FolderDiffItem> items = _filterMode switch
        {
            "Diffs"     => _allItems.Where(i => i.Status != DiffStatus.Same),
            "Same"      => _allItems.Where(i => i.Status == DiffStatus.Same),
            "LeftOnly"  => _allItems.Where(i => i.Status == DiffStatus.LeftOnly),
            "RightOnly" => _allItems.Where(i => i.Status == DiffStatus.RightOnly),
            _           => _allItems
        };

        if (!string.IsNullOrEmpty(search))
            items = items.Where(i => i.DisplayName.ToLower().Contains(search));

        FileList.ItemsSource = items.ToList();
        ApplySort();
        TbStatus.Text = $"{FileList.Items.Count}개 항목 표시 중";
    }

    // ─── 경로 선택 ─────────────────────────────────────────────

    private void BtnLeftBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickFolder(TxtLeftPath.Text);
        if (path != null) { TxtLeftPath.Text = path; RunCompare(); }
    }

    private void BtnRightBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickFolder(TxtRightPath.Text);
        if (path != null) { TxtRightPath.Text = path; RunCompare(); }
    }

    private void BtnLeftUp_Click(object s, RoutedEventArgs e)
    {
        var parent = Path.GetDirectoryName(TxtLeftPath.Text.TrimEnd('\\'));
        if (parent != null) { TxtLeftPath.Text = parent; RunCompare(); }
    }

    private void BtnRightUp_Click(object s, RoutedEventArgs e)
    {
        var parent = Path.GetDirectoryName(TxtRightPath.Text.TrimEnd('\\'));
        if (parent != null) { TxtRightPath.Text = parent; RunCompare(); }
    }

    private void BtnSwap_Click(object s, RoutedEventArgs e)
    {
        (TxtLeftPath.Text, TxtRightPath.Text) = (TxtRightPath.Text, TxtLeftPath.Text);
        if (!string.IsNullOrEmpty(TxtLeftPath.Text)) RunCompare();
    }

    private void BtnCompare_Click(object s, RoutedEventArgs e) => RunCompare();

    private void TxtPath_KeyDown(object s, KeyEventArgs e)
    { if (e.Key == Key.Enter) RunCompare(); }

    private void TxtFilter_KeyDown(object s, KeyEventArgs e)
    { if (e.Key == Key.Enter) RunCompare(); }

    private static string? PickFolder(string? initial)
    {
        var dlg = new OpenFolderDialog { Title = "폴더 선택" };
        if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    // ─── 파일 목록 더블클릭 ────────────────────────────────────

    private void FileList_DoubleClick(object s, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is not FolderDiffItem item) return;

        if (item.IsDirectory)
        {
            if (item.LeftFullPath != null && item.RightFullPath != null)
            {
                TxtLeftPath.Text  = item.LeftFullPath;
                TxtRightPath.Text = item.RightFullPath;
                RunCompare();
            }
        }
        else
        {
            // 파일 확장자로 비교 모드 자동 감지
            var refPath = item.LeftFullPath ?? item.RightFullPath;
            var mode = DetectCompareMode(refPath);
            _main.OpenCompare(mode, item.LeftFullPath, item.RightFullPath);
        }
    }

    private static readonly HashSet<string> _imageExts = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".ico" };

    private static readonly HashSet<string> _textExts = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".cs", ".vb", ".fs", ".js", ".ts", ".jsx", ".tsx", ".py", ".rb", ".go",
          ".java", ".c", ".cpp", ".h", ".hpp", ".rs", ".swift", ".kt", ".md", ".json",
          ".xml", ".xaml", ".html", ".htm", ".css", ".scss", ".less", ".yaml", ".yml",
          ".toml", ".ini", ".cfg", ".conf", ".sh", ".bat", ".cmd", ".ps1", ".sql",
          ".csv", ".log", ".gitignore", ".editorconfig" };

    private static CompareMode DetectCompareMode(string? path)
    {
        if (path == null) return CompareMode.Text;
        var ext = Path.GetExtension(path);
        if (_imageExts.Contains(ext)) return CompareMode.Image;
        if (_textExts.Contains(ext))  return CompareMode.Text;
        // 확장자로 판단 불가 시 파일 앞 8KB 읽어서 바이너리 여부 확인
        if (IsBinaryFile(path)) return CompareMode.Hex;
        return CompareMode.Text;
    }

    private static bool IsBinaryFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[Math.Min(8192, (int)fs.Length)];
            int read = fs.Read(buf, 0, buf.Length);
            return buf.Take(read).Any(b => b == 0);
        }
        catch { return false; }
    }

    // ─── 필터 버튼 ─────────────────────────────────────────────

    private void BtnFilter_Click(object s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string tag)
        {
            _filterMode = tag;
            ApplyFilterAndSearch();
        }
    }

    private void ChkRecurse_Changed(object s, RoutedEventArgs e)
    { if (IsLoaded && !string.IsNullOrEmpty(TxtLeftPath.Text)) RunCompare(); }

    private void ChkContent_Changed(object s, RoutedEventArgs e)
    { if (IsLoaded && !string.IsNullOrEmpty(TxtLeftPath.Text)) RunCompare(); }

    private void TxtSearch_TextChanged(object s, TextChangedEventArgs e) => ApplyFilterAndSearch();
    private void BtnRefresh_Click(object s, RoutedEventArgs e) => RunCompare();

    // ─── 정렬 ─────────────────────────────────────────────────

    private void OnColumnHeaderClick(object s, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader hdr || hdr.Tag is not string col) return;
        if (_sortHeader != null && _sortHeader != hdr)
            _sortHeader.Content = ((string)_sortHeader.Content).TrimEnd(' ', '▲', '▼');
        if (_sortColumn == col) _sortAsc = !_sortAsc;
        else { _sortColumn = col; _sortAsc = true; }
        hdr.Content = ((string)hdr.Content).TrimEnd(' ', '▲', '▼') + (_sortAsc ? " ▲" : " ▼");
        _sortHeader = hdr;
        ApplySort();
    }

    private void ApplySort()
    {
        if (_sortColumn == null || FileList.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(FileList.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription("IsDirectory",
            ListSortDirection.Descending)); // 폴더 먼저
        view.SortDescriptions.Add(new SortDescription(_sortColumn,
            _sortAsc ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    // ─── 파일 작업 ─────────────────────────────────────────────

    private List<FolderDiffItem> SelectedItems()
        => FileList.SelectedItems.Cast<FolderDiffItem>().ToList();

    private void BtnCopyLR_Click(object s, RoutedEventArgs e)
    {
        foreach (var item in SelectedItems().Where(i => i.LeftFullPath != null))
        {
            if (item.IsDirectory)
            {
                string dest = Path.Combine(TxtRightPath.Text, item.RelPath);
                _fops.CopyFolder(item.LeftFullPath!, dest);
            }
            else
            {
                string dest = BuildDestPath(item.LeftFullPath!, TxtLeftPath.Text, TxtRightPath.Text);
                var r = _fops.CopyFile(item.LeftFullPath!, dest);
                if (!r.Success) ShowError(r.Error);
            }
        }
        RunCompare();
    }

    private void BtnCopyRL_Click(object s, RoutedEventArgs e)
    {
        foreach (var item in SelectedItems().Where(i => i.RightFullPath != null))
        {
            if (item.IsDirectory)
            {
                string dest = Path.Combine(TxtLeftPath.Text, item.RelPath);
                _fops.CopyFolder(item.RightFullPath!, dest);
            }
            else
            {
                string dest = BuildDestPath(item.RightFullPath!, TxtRightPath.Text, TxtLeftPath.Text);
                var r = _fops.CopyFile(item.RightFullPath!, dest);
                if (!r.Success) ShowError(r.Error);
            }
        }
        RunCompare();
    }

    private void BtnMoveLR_Click(object s, RoutedEventArgs e)
    {
        foreach (var item in SelectedItems().Where(i => i.LeftFullPath != null))
        {
            string dest = BuildDestPath(item.LeftFullPath!, TxtLeftPath.Text, TxtRightPath.Text);
            var r = item.IsDirectory
                ? _fops.MoveFolder(item.LeftFullPath!, dest)
                : _fops.MoveFile(item.LeftFullPath!, dest);
            if (!r.Success) ShowError(r.Error);
        }
        RunCompare();
    }

    private void BtnMoveRL_Click(object s, RoutedEventArgs e)
    {
        foreach (var item in SelectedItems().Where(i => i.RightFullPath != null))
        {
            string dest = BuildDestPath(item.RightFullPath!, TxtRightPath.Text, TxtLeftPath.Text);
            var r = item.IsDirectory
                ? _fops.MoveFolder(item.RightFullPath!, dest)
                : _fops.MoveFile(item.RightFullPath!, dest);
            if (!r.Success) ShowError(r.Error);
        }
        RunCompare();
    }

    private void BtnDelete_Click(object s, RoutedEventArgs e)
    {
        var items = SelectedItems();
        if (items.Count == 0) return;

        var result = MessageBox.Show(
            $"선택한 {items.Count}개 항목을 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
            "삭제 확인", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        foreach (var item in items)
        {
            if (item.LeftFullPath  != null)
            { if (item.IsDirectory) _fops.DeleteFolder(item.LeftFullPath); else _fops.DeleteFile(item.LeftFullPath); }
            if (item.RightFullPath != null)
            { if (item.IsDirectory) _fops.DeleteFolder(item.RightFullPath); else _fops.DeleteFile(item.RightFullPath); }
        }
        RunCompare();
    }

    private void BtnCompareSelected_Click(object s, RoutedEventArgs e)
    {
        var files = FileList.SelectedItems.Cast<FolderDiffItem>()
                            .Where(i => !i.IsDirectory).ToList();
        if (files.Count == 2)
        {
            // 두 파일을 직접 비교
            string left  = files[0].LeftFullPath ?? files[0].RightFullPath ?? "";
            string right = files[1].LeftFullPath ?? files[1].RightFullPath ?? "";
            var mode = DetectCompareMode(left);
            _main.OpenCompare(mode, left, right);
        }
        else if (files.Count == 1)
        {
            // 하나만 선택 시 기존 방식
            var item = files[0];
            var mode = DetectCompareMode(item.LeftFullPath ?? item.RightFullPath);
            _main.OpenCompare(mode, item.LeftFullPath, item.RightFullPath);
        }
        else
        {
            MessageBox.Show("파일을 1~2개 선택하세요.\nCtrl+클릭으로 다른 두 파일을 선택할 수 있습니다.",
                "선택 비교", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnOpenExplorer_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        _fops.OpenInExplorer(item?.LeftFullPath ?? TxtLeftPath.Text);
    }

    private void BtnOpenTextDiff_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        if (item is { IsDirectory: false })
            _main.OpenCompare(CompareMode.Text, item.LeftFullPath, item.RightFullPath);
    }

    private void BtnOpenHexDiff_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        if (item is { IsDirectory: false })
            _main.OpenCompare(CompareMode.Hex, item.LeftFullPath, item.RightFullPath);
    }

    private void BtnOpenImageDiff_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        if (item is { IsDirectory: false })
            _main.OpenCompare(CompareMode.Image, item.LeftFullPath, item.RightFullPath);
    }

    private void BtnOpenLeftExplorer_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        _fops.OpenInExplorer(item?.LeftFullPath ?? TxtLeftPath.Text);
    }

    private void BtnOpenRightExplorer_Click(object s, RoutedEventArgs e)
    {
        var item = FileList.SelectedItem as FolderDiffItem;
        _fops.OpenInExplorer(item?.RightFullPath ?? TxtRightPath.Text);
    }

    private static string BuildDestPath(string srcFile, string srcRoot, string destRoot)
    {
        var rel = Path.GetRelativePath(srcRoot, srcFile);
        return Path.Combine(destRoot, rel);
    }

    private static void ShowError(string? msg)
        => MessageBox.Show(msg ?? "오류가 발생했습니다.", "작업 오류", MessageBoxButton.OK, MessageBoxImage.Error);
}
