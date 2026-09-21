using System.Diagnostics;
using AIPForge.Models;
using AIPForge.Services;
using AIPForge.Views;
using Microsoft.Win32;

namespace AIPForge;

/// <summary>메인 창 — 파일 열기·저장·탐색 트리 (표 편집 · 개요 · 빌드 · 폴더는 partial 파일로 분리)</summary>
public partial class MainWindow : Window
{
    private enum NavKind { Overview, Component, Raw, Search, Compare }

    private sealed record NavTarget(NavKind Kind, AipElement? Component = null);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly Dictionary<AipElement, TreeViewItem> _navItems = [];
    private readonly Dictionary<AipElement, TextBlock> _navCounts = [];

    private AipDocument? _doc;
    private string _diskText = "";
    private DateTime _diskWriteTimeUtc;
    private DateTime _ignoredWriteTimeUtc;
    private bool _dirty;
    private bool _checkingExternal;
    private bool _suppressNav;
    private string? _cliPath;
    private NavTarget? _current;
    private TreeViewItem? _searchNavItem;
    private TreeViewItem? _rawNavItem;
    private TreeViewItem? _overviewNavItem;

    public MainWindow(string? startFile)
    {
        InitializeComponent();
        DarkTitle.Apply(this);

        ColNav.Width = new GridLength(Math.Clamp(_settings.NavWidth, 220, 600));
        ChkSyncBeforeBuild.IsChecked = _settings.RefreshSyncBeforeBuild;
        _cliPath = AdvInstaller.LocateCli(_settings.AdvInstallerCliPath);

        TxtGlobalSearch.TextChanged += (_, _) =>
            TxtSearchHint.Visibility = TxtGlobalSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateAiStatus();
        RenderRecent();
        UpdateUiState();
        UpdateTitle();

        if (startFile != null) Loaded += async (_, _) => await OpenFileAsync(startFile);
    }

    // ── 열기 ────────────────────────────────────────────────────────

    private async void BtnOpen_Click(object sender, RoutedEventArgs e) => await PromptOpenAsync();

    private async void CmdOpen_Executed(object sender, ExecutedRoutedEventArgs e) => await PromptOpenAsync();

    private async Task PromptOpenAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Advanced Installer 프로젝트 (*.aip)|*.aip|모든 파일 (*.*)|*.*",
            InitialDirectory = _doc?.Directory ?? RecentDirectory(),
        };
        if (dlg.ShowDialog(this) == true) await OpenFileAsync(dlg.FileName);
    }

    private string RecentDirectory()
    {
        var first = _settings.RecentFiles.FirstOrDefault();
        var dir = first == null ? null : Path.GetDirectoryName(first);
        return dir != null && System.IO.Directory.Exists(dir) ? dir : "";
    }

    private async Task<bool> OpenFileAsync(string path)
    {
        if (!ConfirmDiscard()) return false;

        SetStatus($"여는 중... {path}");
        AipDocument doc;
        try
        {
            doc = await Task.Run(() => AipDocument.Load(path));
        }
        catch (Exception ex)
        {
            SetStatus("열기 실패");
            DarkDialog.Info(this, $"파일을 열 수 없습니다.\n\n{path}\n\n{ex.Message}");
            return false;
        }

        _doc = doc;
        _diskText = doc.LoadedText;
        _diskWriteTimeUtc = _ignoredWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        _dirty = false;
        _tables.Clear();
        _validation = null;

        _settings.AddRecent(path);
        _settings.Save();
        RenderRecent();

        PopulateBuildCombo();
        BuildNav();
        SelectNav(_overviewNavItem);
        UpdateTitle();
        UpdateUiState();
        SetStatus($"열림 — {doc.ProductName} {doc.ProductVersion} · 테이블 {doc.Components.Count()}개 · AI {doc.AiVersion}");

        await LoadFolderAsync(path);
        return true;
    }

    /// <summary>편집 내용을 버리고 디스크에서 다시 읽기 (보고 있던 테이블 유지)</summary>
    private async Task ReloadFromDiskAsync()
    {
        if (_doc?.FilePath == null) return;
        var path = _doc.FilePath;
        var cid = _current?.Component?.Get("cid");
        var kind = _current?.Kind ?? NavKind.Overview;

        _dirty = false;
        _rawDirty = false;
        if (!await OpenFileAsync(path)) return;

        if (kind == NavKind.Component && cid != null)
        {
            var comp = _doc?.Components.FirstOrDefault(c => c.Get("cid") == cid);
            if (comp != null && _navItems.TryGetValue(comp, out var item)) SelectNav(item);
        }
        else if (kind == NavKind.Raw) SelectNav(_rawNavItem);
    }

    private async void BtnReload_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        CommitPendingEdits();
        if (_dirty && !DarkDialog.Confirm(this, "저장하지 않은 편집 내용을 모두 버리고 디스크에서 다시 읽을까요?", "다시 불러오기"))
            return;
        await ReloadFromDiskAsync();
    }

    // ── 저장 ────────────────────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveDocument();

    private void CmdSave_Executed(object sender, ExecutedRoutedEventArgs e) => SaveDocument();

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e) => SaveAs();

    private void CmdSaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private void SaveAs()
    {
        if (_doc == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Advanced Installer 프로젝트 (*.aip)|*.aip",
            InitialDirectory = _doc.Directory ?? "",
            FileName = Path.GetFileName(_doc.FilePath) ?? "project.aip",
        };
        if (dlg.ShowDialog(this) != true) return;
        if (SaveDocument(dlg.FileName)) _ = LoadFolderAsync(dlg.FileName);
    }

    /// <summary>저장 — 외부 변경 확인 → 로컬 백업 → 기록</summary>
    private bool SaveDocument(string? path = null)
    {
        if (_doc == null) return false;
        if (!CommitPendingEdits()) return false;

        var target = path ?? _doc.FilePath!;
        try
        {
            if (path == null && File.Exists(target) && File.GetLastWriteTimeUtc(target) != _diskWriteTimeUtc &&
                !DarkDialog.Confirm(this,
                    "디스크의 파일이 AIP.Forge 밖에서 변경되었습니다.\n덮어쓰면 그 변경 내용은 사라집니다. 계속할까요?",
                    "덮어쓰기"))
                return false;

            var backup = Backup.Create(target);
            _doc.Save(target);
            _diskText = _doc.Serialize();
            _diskWriteTimeUtc = _ignoredWriteTimeUtc = File.GetLastWriteTimeUtc(target);
            _dirty = false;

            if (path != null)
            {
                _settings.AddRecent(target);
                _settings.Save();
                RenderRecent();
            }
            UpdateTitle();
            RefreshFolderItem(target);
            SetStatus($"저장됨 {DateTime.Now:HH:mm:ss} — {target}" + (backup != null ? "  (이전 파일은 백업 폴더에 보관)" : ""));
            return true;
        }
        catch (Exception ex)
        {
            DarkDialog.Info(this, $"저장하지 못했습니다.\n\n{target}\n\n{ex.Message}");
            return false;
        }
    }

    /// <summary>진행 중인 편집(표 셀 · XML 원문 · 개요 입력칸)을 문서에 반영. XML 오류 시 false</summary>
    private bool CommitPendingEdits()
    {
        if (_doc == null) return true;
        if (PnlTable.Visibility == Visibility.Visible)
        {
            TableGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }
        if (PnlOverview.Visibility == Visibility.Visible) ApplyOverviewFields();
        if (_rawDirty && !TryApplyRaw(showError: true)) return false;
        return true;
    }

    /// <summary>저장 안 된 변경이 있으면 저장/버림/취소를 묻는다. 계속 진행해도 되면 true</summary>
    private bool ConfirmDiscard()
    {
        if (_doc == null) return true;
        CommitPendingEdits();
        if (!_dirty && !_rawDirty) return true;

        return DarkDialog.SaveChanges(this, Path.GetFileName(_doc.FilePath) ?? "") switch
        {
            DialogChoice.Primary => SaveDocument(),
            DialogChoice.Secondary => true,
            _ => false,
        };
    }

    private void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        UpdateTitle();
    }

    // ── 변경 사항 · 외부 도구 ───────────────────────────────────────

    private void BtnDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        if (!CommitPendingEdits()) return;
        var win = new DiffWindow(_diskText, _doc.Serialize(), canSave: true) { Owner = this };
        win.ShowDialog();
        if (win.SaveRequested) SaveDocument();
    }

    private void BtnOpenAi_Click(object sender, RoutedEventArgs e)
    {
        if (_doc?.FilePath == null || !EnsureCli()) return;
        CommitPendingEdits();
        if (_dirty)
        {
            if (!DarkDialog.Confirm(this, "저장하지 않은 변경 사항은 Advanced Installer에 보이지 않습니다.\n저장하고 열까요?", "저장 후 열기"))
                return;
            if (!SaveDocument()) return;
        }
        try
        {
            AdvInstaller.OpenInGui(_cliPath!, _doc.FilePath);
            SetStatus("Advanced Installer에서 여는 중 — 그쪽에서 저장하면 이 창으로 돌아올 때 다시 불러올지 묻습니다.");
        }
        catch (Exception ex)
        {
            DarkDialog.Info(this, ex.Message);
        }
    }

    /// <summary>창으로 돌아왔을 때 다른 프로그램(AI 등)이 파일을 바꿨는지 확인</summary>
    private async void Window_Activated(object? sender, EventArgs e)
    {
        if (_checkingExternal || _building || _doc?.FilePath == null || !File.Exists(_doc.FilePath)) return;
        var now = File.GetLastWriteTimeUtc(_doc.FilePath);
        if (now == _diskWriteTimeUtc || now == _ignoredWriteTimeUtc) return;

        _checkingExternal = true;
        try
        {
            var msg = _dirty
                ? "다른 프로그램에서 이 파일이 변경되었습니다.\n다시 불러오면 AIP.Forge에서 편집한 내용은 사라집니다. 다시 불러올까요?"
                : "다른 프로그램에서 이 파일이 변경되었습니다. 다시 불러올까요?";
            if (DarkDialog.Confirm(this, msg, "다시 불러오기"))
            {
                _dirty = false;
                _rawDirty = false;
                await ReloadFromDiskAsync();
            }
            else _ignoredWriteTimeUtc = now;
        }
        finally
        {
            _checkingExternal = false;
        }
    }

    // ── 탐색 트리 ───────────────────────────────────────────────────

    private void BuildNav()
    {
        _suppressNav = true;
        NavTree.Items.Clear();
        _navItems.Clear();
        _navCounts.Clear();
        _searchNavItem = null;
        if (_doc == null)
        {
            _suppressNav = false;
            return;
        }

        _overviewNavItem = MakeNavItem("개요", null, new NavTarget(NavKind.Overview), bold: true);
        NavTree.Items.Add(_overviewNavItem);
        if (_folderItems.Count > 1)
            NavTree.Items.Add(MakeNavItem("폴더 비교", null, new NavTarget(NavKind.Compare), bold: true));

        var groups = _doc.Components
            .Select(c => (el: c, meta: ComponentCatalog.Get(AipDocument.ShortCid(c))))
            .GroupBy(x => x.meta.Category)
            .OrderBy(g => Array.IndexOf(ComponentCatalog.CategoryOrder, g.Key));

        foreach (var g in groups)
        {
            var cat = new TreeViewItem { Header = MakeCategoryHeader(g.Key, g.Count()), Focusable = true };
            System.Windows.Automation.AutomationProperties.SetName(cat, g.Key);
            foreach (var (el, meta) in g.OrderBy(x => x.meta.Title, StringComparer.CurrentCulture))
            {
                var item = MakeNavItem(meta.Title, AipDocument.Rows(el).Count(), new NavTarget(NavKind.Component, el));
                item.ToolTip = $"{meta.Description}\n{el.Get("cid")}";
                cat.Items.Add(item);
                _navItems[el] = item;
            }
            NavTree.Items.Add(cat);
        }

        _rawNavItem = MakeNavItem("XML 원문", null, new NavTarget(NavKind.Raw), bold: true);
        NavTree.Items.Add(_rawNavItem);
        _suppressNav = false;
    }

    private TreeViewItem MakeNavItem(string title, int? count, NavTarget target, bool bold = false)
    {
        var dock = new DockPanel { LastChildFill = true };
        if (count != null)
        {
            var tb = new TextBlock
            {
                Text = count.Value.ToString("N0"),
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11.5,
                Margin = new Thickness(8, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(tb, Dock.Right);
            dock.Children.Add(tb);
            if (target.Component != null) _navCounts[target.Component] = tb;
        }
        dock.Children.Add(new TextBlock
        {
            Text = title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        });
        var item = new TreeViewItem { Header = dock, Tag = target };
        System.Windows.Automation.AutomationProperties.SetName(item, title);
        return item;
    }

    private TextBlock MakeCategoryHeader(string category, int count) => new()
    {
        Text = $"{category}  ({count})",
        Foreground = (Brush)FindResource("TextSecondary"),
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
    };

    private void SelectNav(TreeViewItem? item)
    {
        if (item == null) return;
        if (item.IsSelected) ShowTarget(item.Tag as NavTarget);
        else item.IsSelected = true;
        item.BringIntoView();
    }

    private void NavTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressNav) return;
        if (e.NewValue is not TreeViewItem { Tag: NavTarget target }) return;

        // XML 원문 편집 중에 다른 화면으로 이동하면 먼저 반영
        if (_current?.Kind == NavKind.Raw && target.Kind != NavKind.Raw && _rawDirty && !TryApplyRaw(showError: false))
        {
            if (!DarkDialog.Confirm(this, "XML 원문에 오류가 있어 적용할 수 없습니다.\n편집 내용을 버리고 이동할까요?", "버리고 이동"))
            {
                Dispatcher.BeginInvoke(() => { _suppressNav = true; if (_rawNavItem != null) _rawNavItem.IsSelected = true; _suppressNav = false; });
                return;
            }
            _rawDirty = false;
        }

        // 표 편집 중이면 커밋
        if (_current?.Kind == NavKind.Component) TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_current?.Kind == NavKind.Overview) ApplyOverviewFields();

        // 원문 적용으로 트리가 다시 만들어졌다면 대상 요소도 새 문서에서 다시 찾는다
        ShowTarget(target);
    }

    private void ShowTarget(NavTarget? target)
    {
        if (target == null || _doc == null) return;
        _current = target;

        PnlWelcome.Visibility = Visibility.Collapsed;
        PnlOverview.Visibility = Visibility.Collapsed;
        PnlTable.Visibility = Visibility.Collapsed;
        PnlRaw.Visibility = Visibility.Collapsed;
        PnlSearch.Visibility = Visibility.Collapsed;
        PnlCompare.Visibility = Visibility.Collapsed;

        switch (target.Kind)
        {
            case NavKind.Overview:
                RenderOverview();
                PnlOverview.Visibility = Visibility.Visible;
                break;
            case NavKind.Component:
                ShowTable(target.Component!);
                break;
            case NavKind.Raw:
                LoadRaw();
                PnlRaw.Visibility = Visibility.Visible;
                break;
            case NavKind.Search:
                PnlSearch.Visibility = Visibility.Visible;
                break;
            case NavKind.Compare:
                PnlCompare.Visibility = Visibility.Visible;
                _ = RenderCompareAsync();
                break;
        }
    }

    private void UpdateNavCount(AipElement component)
    {
        if (_navCounts.TryGetValue(component, out var tb))
            tb.Text = AipDocument.Rows(component).Count().ToString("N0");
    }

    // ── 끌어다 놓기 · 최근 파일 ─────────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedAip(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        var path = DroppedAip(e);
        if (path != null) await OpenFileAsync(path);
    }

    private static string? DroppedAip(DragEventArgs e)
        => e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(f => f.EndsWith(".aip", StringComparison.OrdinalIgnoreCase))
            : null;

    private void RenderRecent()
    {
        LstRecent.Items.Clear();
        foreach (var path in _settings.RecentFiles)
        {
            var btn = new Button
            {
                Content = path,
                Style = (Style)FindResource("LinkButton"),
                Margin = new Thickness(0, 3, 0, 3),
                ToolTip = File.Exists(path) ? path : "파일이 없습니다 (이동 또는 삭제됨)",
                Opacity = File.Exists(path) ? 1 : 0.45,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            btn.Click += async (_, _) => await OpenFileAsync(path);
            LstRecent.Items.Add(btn);
        }
        TxtNoRecent.Visibility = _settings.RecentFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 상태 표시 ───────────────────────────────────────────────────

    private void UpdateTitle()
    {
        var name = _doc?.FilePath == null ? null : Path.GetFileName(_doc.FilePath);
        Title = name == null ? "AIP.Forge" : $"{name}{(_dirty ? " *" : "")} — AIP.Forge";
        BdgDirty.Visibility = _dirty ? Visibility.Visible : Visibility.Collapsed;
        TxtFilePath.Text = _doc?.FilePath ?? "";
        TxtFilePath.ToolTip = _doc?.FilePath;
    }

    private void UpdateUiState()
    {
        var hasDoc = _doc != null;
        BtnSave.IsEnabled = BtnSaveAs.IsEnabled = BtnDiff.IsEnabled = BtnReload.IsEnabled = hasDoc && !_building;
        BtnVersion.IsEnabled = hasDoc && !_building;
        BtnBuild.IsEnabled = BtnRebuild.IsEnabled = CmbBuild.IsEnabled = hasDoc && !_building;
        BtnOpenAi.IsEnabled = hasDoc;
        BtnBatchBuild.IsEnabled = BtnBatchVersion.IsEnabled = !_building;
        TxtGlobalSearch.IsEnabled = hasDoc;
        BtnCancelBuild.Visibility = _building ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAiStatus()
    {
        if (_cliPath == null)
        {
            BtnAiPath.Content = "Advanced Installer를 찾지 못함 — 클릭해서 위치 지정";
            BtnAiPath.Foreground = (Brush)FindResource("DangerBrush");
        }
        else
        {
            BtnAiPath.Content = $"Advanced Installer {AdvInstaller.InstalledVersion(_cliPath)} CLI 연결됨";
            BtnAiPath.Foreground = (Brush)FindResource("TextSecondary");
            BtnAiPath.ToolTip = _cliPath;
        }
    }

    private void BtnAiPath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "AdvancedInstaller.com|AdvancedInstaller.com|실행 파일 (*.com;*.exe)|*.com;*.exe",
            InitialDirectory = _cliPath != null ? Path.GetDirectoryName(_cliPath) : @"C:\Program Files (x86)\Caphyon",
        };
        if (dlg.ShowDialog(this) != true) return;
        _cliPath = dlg.FileName;
        _settings.AdvInstallerCliPath = dlg.FileName;
        _settings.Save();
        UpdateAiStatus();
    }

    /// <summary>CLI 경로 확인 — 없으면 안내 후 false</summary>
    private bool EnsureCli()
    {
        if (_cliPath != null && File.Exists(_cliPath)) return true;
        DarkDialog.Info(this, "Advanced Installer(AdvancedInstaller.com)를 찾지 못했습니다.\n아래 상태 표시줄 오른쪽을 클릭해 위치를 지정하세요.\n\n예: C:\\Program Files (x86)\\Caphyon\\Advanced Installer 18.3\\bin\\x86\\AdvancedInstaller.com");
        return false;
    }

    private void SetStatus(string text) => TxtStatus.Text = text;

    private static void OpenInExplorer(string path)
    {
        try
        {
            if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (System.IO.Directory.Exists(path)) Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch
        {
            // 탐색기 실행 실패는 무시
        }
    }

    // ── 창 종료 · 레이아웃 저장 ─────────────────────────────────────

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_building && !DarkDialog.Confirm(this, "빌드가 진행 중입니다. 중지하고 닫을까요?", "중지하고 닫기"))
        {
            e.Cancel = true;
            return;
        }
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }
        _buildCts?.Cancel();
        _settings.NavWidth = ColNav.ActualWidth;
        _settings.Save();
    }

    private void NavSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _settings.NavWidth = ColNav.ActualWidth;
        _settings.Save();
    }
}
