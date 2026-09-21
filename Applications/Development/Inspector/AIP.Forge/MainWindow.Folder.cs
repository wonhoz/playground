using System.Data;
using System.Windows.Data;
using AIPForge.Models;
using AIPForge.Services;
using AIPForge.Views;

namespace AIPForge;

/// <summary>같은 폴더의 AIP 목록 — 전환 · 일괄 버전 변경 · 일괄 빌드 · 비교</summary>
public partial class MainWindow
{
    private readonly ObservableCollection<FolderItem> _folderItems = [];
    private string? _folderDir;
    private bool _suppressFileSelection;

    public sealed class FolderItem(string path) : INotifyPropertyChanged
    {
        public string Path { get; } = path;
        public string FileName { get; } = System.IO.Path.GetFileName(path);

        private string _productName = "읽는 중...";
        public string ProductName { get => _productName; set { _productName = value; Notify(nameof(ProductName)); } }

        private string _version = "";
        public string Version { get => _version; set { _version = value; Notify(nameof(Version)); } }

        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set { _isChecked = value; Notify(nameof(IsChecked)); } }

        private bool _isCurrent;
        public bool IsCurrent { get => _isCurrent; set { _isCurrent = value; Notify(nameof(IsCurrent)); Notify(nameof(Weight)); } }

        public FontWeight Weight => IsCurrent ? FontWeights.SemiBold : FontWeights.Normal;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>열린 파일의 폴더를 훑어 AIP 목록 구성 (2개 이상일 때만 표시)</summary>
    private async Task LoadFolderAsync(string currentPath)
    {
        var dir = Path.GetDirectoryName(currentPath);
        if (dir == null) return;

        if (!string.Equals(dir, _folderDir, StringComparison.OrdinalIgnoreCase))
        {
            _folderDir = dir;
            _folderItems.Clear();
            string[] files;
            try
            {
                files = await Task.Run(() => System.IO.Directory.GetFiles(dir, "*.aip"));
            }
            catch
            {
                files = [];
            }
            foreach (var f in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) _folderItems.Add(new FolderItem(f));
            LstFiles.ItemsSource = _folderItems;
            TxtFolderTitle.Text = $"같은 폴더의 AIP ({_folderItems.Count})";
            TxtFolderTitle.ToolTip = dir;

            foreach (var item in _folderItems.ToList()) _ = PeekAsync(item);
            BuildNavKeepSelection();
        }

        PnlFolder.Visibility = _folderItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        MarkCurrentFolderItem(currentPath);
    }

    /// <summary>폴더 목록이 바뀌어 "폴더 비교" 항목이 생기거나 없어질 때 트리를 다시 만든다</summary>
    private void BuildNavKeepSelection()
    {
        if (_doc == null) return;
        var target = _current;
        BuildNav();
        if (target?.Kind == NavKind.Component && target.Component != null && _navItems.TryGetValue(target.Component, out var item))
            SelectNav(item);
        else if (target?.Kind == NavKind.Raw) SelectNav(_rawNavItem);
        else SelectNav(_overviewNavItem);
    }

    private static async Task PeekAsync(FolderItem item)
    {
        try
        {
            var doc = await Task.Run(() => AipDocument.Load(item.Path));
            item.ProductName = doc.ProductName;
            item.Version = doc.ProductVersion;
        }
        catch
        {
            item.ProductName = "(읽을 수 없음)";
        }
    }

    private void MarkCurrentFolderItem(string currentPath)
    {
        _suppressFileSelection = true;
        foreach (var it in _folderItems)
        {
            it.IsCurrent = string.Equals(it.Path, currentPath, StringComparison.OrdinalIgnoreCase);
            if (it.IsCurrent) LstFiles.SelectedItem = it;
        }
        _suppressFileSelection = false;
    }

    /// <summary>저장·빌드 후 목록의 제품명·버전을 디스크 기준으로 갱신</summary>
    private void RefreshFolderItem(string path)
    {
        var item = _folderItems.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item != null) _ = PeekAsync(item);
    }

    /// <summary>현재 편집 중인 문서 값(저장 전)을 목록에 표시</summary>
    private void RefreshCurrentFolderItem()
    {
        if (_doc?.FilePath == null) return;
        var item = _folderItems.FirstOrDefault(i => string.Equals(i.Path, _doc.FilePath, StringComparison.OrdinalIgnoreCase));
        if (item == null) return;
        item.ProductName = _doc.ProductName;
        item.Version = _doc.ProductVersion;
    }

    private async void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFileSelection || LstFiles.SelectedItem is not FolderItem item) return;
        if (string.Equals(item.Path, _doc?.FilePath, StringComparison.OrdinalIgnoreCase)) return;

        if (!await OpenFileAsync(item.Path) && _doc?.FilePath != null)
            MarkCurrentFolderItem(_doc.FilePath); // 취소 시 선택 되돌림
    }

    private void ChkAllFiles_Click(object sender, RoutedEventArgs e)
    {
        var check = ChkAllFiles.IsChecked == true;
        foreach (var it in _folderItems) it.IsChecked = check;
    }

    private void FileCheck_Click(object sender, RoutedEventArgs e)
    {
        var count = _folderItems.Count(i => i.IsChecked);
        ChkAllFiles.IsChecked = count == 0 ? false : count == _folderItems.Count ? true : null;
    }

    private List<FolderItem> CheckedItems(string action)
    {
        var list = _folderItems.Where(i => i.IsChecked).ToList();
        if (list.Count == 0) DarkDialog.Info(this, $"{action}할 파일을 왼쪽 목록에서 체크하세요.");
        return list;
    }

    // ── 일괄 버전 변경 ──────────────────────────────────────────────

    private void BtnBatchVersion_Click(object sender, RoutedEventArgs e)
    {
        var items = CheckedItems("버전을 변경");
        if (items.Count == 0 || !CommitPendingEdits()) return;

        var includesCurrent = items.Any(i => string.Equals(i.Path, _doc?.FilePath, StringComparison.OrdinalIgnoreCase));
        var listText = string.Join("\n", items.Take(12).Select(i => $"  · {i.FileName}  ({i.Version})"));
        if (items.Count > 12) listText += $"\n  … 외 {items.Count - 12}개";
        var baseVersion = items.Select(i => i.Version).Where(v => v.Length > 0)
            .OrderByDescending(v => Version.TryParse(v, out var ver) ? ver : new Version(0, 0)).FirstOrDefault() ?? "1.0.0";

        var dlg = new VersionDialog($"{items.Count}개 파일의 버전을 한 번에 바꿉니다. 각 파일은 바로 저장됩니다.\n\n{listText}",
            baseVersion, _settings.NewProductCodeOnVersionChange) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        if (includesCurrent && _dirty &&
            !DarkDialog.Confirm(this, "현재 열린 파일에 저장하지 않은 다른 편집 내용이 있습니다.\n버전 변경과 함께 저장할까요?", "함께 저장"))
            return;

        _settings.NewProductCodeOnVersionChange = dlg.NewProductCode;
        _settings.Save();

        var failed = new List<string>();
        foreach (var it in items)
        {
            try
            {
                if (string.Equals(it.Path, _doc?.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _doc!.SetVersion(dlg.Version, dlg.NewProductCode);
                    MarkDirty();
                    InvalidateTables();
                    if (!SaveDocument()) failed.Add($"{it.FileName}: 저장 취소");
                    if (_current?.Kind == NavKind.Overview) RenderOverview();
                }
                else
                {
                    var doc = AipDocument.Load(it.Path);
                    if (!doc.SetVersion(dlg.Version, dlg.NewProductCode))
                    {
                        failed.Add($"{it.FileName}: ProductVersion 없음");
                        continue;
                    }
                    Backup.Create(it.Path);
                    doc.Save();
                    RefreshFolderItem(it.Path);
                }
            }
            catch (Exception ex)
            {
                failed.Add($"{it.FileName}: {ex.Message}");
            }
        }

        var okCount = items.Count - failed.Count;
        SetStatus($"일괄 버전 변경 → {dlg.Version}: 성공 {okCount} · 실패 {failed.Count}");
        if (failed.Count > 0) DarkDialog.Info(this, "일부 파일을 변경하지 못했습니다.\n\n" + string.Join("\n", failed));
    }

    // ── 일괄 빌드 ───────────────────────────────────────────────────

    private async void BtnBatchBuild_Click(object sender, RoutedEventArgs e)
    {
        var items = CheckedItems("빌드");
        if (items.Count == 0 || _building || !EnsureCli() || !CommitPendingEdits()) return;

        if (_dirty && items.Any(i => string.Equals(i.Path, _doc?.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            if (!DarkDialog.Confirm(this, "현재 파일의 변경 사항을 저장하고 빌드할까요?", "저장 후 빌드")) return;
            if (!SaveDocument()) return;
        }
        if (!DarkDialog.Confirm(this, $"{items.Count}개 파일을 차례로 빌드합니다.\n\n" +
                                      string.Join("\n", items.Select(i => $"  · {i.FileName}  {i.Version}")), "빌드 시작"))
            return;

        ShowLog(clear: true);
        await RunBuildAsync(items.Select(i => i.Path).ToList(), rebuild: false, buildName: null);
        await ReloadIfChangedOnDiskAsync();
    }

    // ── 비교 ────────────────────────────────────────────────────────

    private void BtnCompareTwo_Click(object sender, RoutedEventArgs e)
    {
        var items = _folderItems.Where(i => i.IsChecked).ToList();
        if (items.Count != 2)
        {
            DarkDialog.Info(this, "비교할 파일 2개를 왼쪽 목록에서 체크하세요.");
            return;
        }
        try
        {
            var a = AipDocument.Load(items[0].Path).LoadedText;
            var b = AipDocument.Load(items[1].Path).LoadedText;
            var win = new DiffWindow(a, b, canSave: false)
            {
                Owner = this,
                Title = $"{items[0].FileName}  ↔  {items[1].FileName}",
            };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            DarkDialog.Info(this, ex.Message);
        }
    }

    /// <summary>폴더의 모든 AIP 핵심 설정을 나란히 비교 (현재 문서는 편집 중인 값 기준)</summary>
    private async Task RenderCompareAsync()
    {
        GridCompare.ItemsSource = null;
        var items = _folderItems.ToList();
        var current = _doc;

        var docs = await Task.Run(() => items.Select(i =>
        {
            if (current != null && string.Equals(i.Path, current.FilePath, StringComparison.OrdinalIgnoreCase)) return current;
            try { return AipDocument.Load(i.Path); }
            catch { return null; }
        }).ToList());

        (string label, Func<AipDocument, string> get)[] fields =
        [
            ("제품명", d => d.ProductName),
            ("버전", d => d.ProductVersion),
            ("제조사", d => d.GetProperty("Manufacturer") ?? ""),
            ("제어판 표시 이름", d => d.GetProperty("AI_PRODUCTNAME_ARP") ?? ""),
            ("UpgradeCode", d => d.GetProperty("UpgradeCode") ?? ""),
            ("ProductCode", d => d.FirstProductCode() ?? ""),
            ("빌드 출력 폴더", d => string.Join(" | ", d.Builds().Select(b => b.PackageFolder))),
            ("출력 파일", d => string.Join(" | ", d.Builds().Select(b => b.OutputFileName))),
            ("플랫폼", d => string.Join(" | ", d.Builds().Select(b => b.Platform))),
            ("동기화 원본", d => string.Join(" | ", d.SyncFolders().Select(s => s.SourcePath))),
            ("파일 수", d => d.RowCount("MsiFilesComponent").ToString()),
            ("구성 요소 수", d => d.RowCount("MsiCompsComponent").ToString()),
            ("바로가기 수", d => d.RowCount("MsiShortsComponent").ToString()),
            ("서비스", d => string.Join(", ", AipDocument.Rows(d.FindComponent("MsiServInstComponent")).Select(r => r.Get("Name")))),
            ("선행 조건", d => string.Join(", ", AipDocument.Rows(d.FindComponent("PreReqComponent")).Select(r => r.Attributes.FirstOrDefault()?.Value))),
            ("AI 버전", d => d.AiVersion),
        ];

        var table = new DataTable();
        table.Columns.Add("항목");
        table.Columns.Add("차이");
        foreach (var it in items) table.Columns.Add(it.FileName);

        foreach (var (label, get) in fields)
        {
            var row = table.NewRow();
            row["항목"] = label;
            var values = new List<string>();
            for (var i = 0; i < items.Count; i++)
            {
                var v = docs[i] == null ? "(읽기 실패)" : get(docs[i]!);
                row[items[i].FileName] = v;
                values.Add(v);
            }
            row["차이"] = values.Distinct().Count() > 1 ? "●" : "";
            table.Rows.Add(row);
        }
        GridCompare.ItemsSource = table.DefaultView;
    }

    private void GridCompare_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 파일명에 점(.)이 있어 기본 바인딩 경로가 깨지므로 인덱서 경로로 교체
        if (e.Column is DataGridTextColumn col)
        {
            col.Binding = new Binding($"[{e.PropertyName}]");
            col.ElementStyle = (Style)FindResource(e.PropertyName == "항목" ? "CellKeyText" : "CellText");
            col.MaxWidth = 380;
            if (e.PropertyName == "차이")
            {
                col.Width = 48;
                col.ElementStyle = new Style(typeof(TextBlock), (Style)FindResource("CellText"))
                {
                    Setters =
                    {
                        new Setter(TextBlock.ForegroundProperty, FindResource("AccentBrush")),
                        new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                    },
                };
            }
        }
    }
}
