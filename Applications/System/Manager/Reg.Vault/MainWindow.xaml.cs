using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RegVault.Models;
using RegVault.Services;

namespace RegVault;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<RegNode>     _roots     = new();
    private readonly ObservableCollection<Bookmark>    _bookmarks = new();
    private readonly ObservableCollection<RegSnapshot> _snapshots = new();

    private RegNode?    _selectedNode;
    private RegValue?   _selectedValue;
    private readonly List<string> _navHistory = new();
    private int _navIndex = -1;

    private CancellationTokenSource? _searchCts;
    private bool _initialized = false;

    public MainWindow()
    {
        InitializeComponent();
        App.ApplyDarkTitlebar(this);

        // 루트 하이브 로드
        foreach (var node in RegNode.CreateHiveRoots())
        {
            node.LoadChildren();
            _roots.Add(node);
        }
        RegTree.ItemsSource = _roots;

        // 북마크 로드
        foreach (var bm in BookmarkStore.Load())
            _bookmarks.Add(bm);
        LstBookmarks.ItemsSource = _bookmarks;

        // 스냅샷 목록 로드
        LoadSnapshots();
        CmbSnapOld.ItemsSource = _snapshots;
        CmbSnapNew.ItemsSource = _snapshots;

        _initialized = true;
    }

    // ── 트리 ────────────────────────────────────────────────────────
    private void RegTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (!_initialized) return;
        if (e.NewValue is not RegNode node) return;
        SelectNode(node);
    }

    private void TreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is RegNode node)
            node.LoadChildren();
    }

    private void SelectNode(RegNode node, bool addHistory = true)
    {
        _selectedNode  = node;
        _selectedValue = null;

        // 경로 표시
        var hiveName = RegistryService.HiveDisplayName(node.Hive);
        TxtPath.Text = string.IsNullOrEmpty(node.FullPath)
            ? hiveName
            : $"{hiveName}\\{node.FullPath}";

        // 히스토리
        if (addHistory)
        {
            if (_navIndex < _navHistory.Count - 1)
                _navHistory.RemoveRange(_navIndex + 1, _navHistory.Count - _navIndex - 1);
            _navHistory.Add(TxtPath.Text);
            _navIndex = _navHistory.Count - 1;
        }

        // 값 로드
        var values = RegistryService.GetValues(node.Hive, node.FullPath);
        DgValues.ItemsSource = values;

        ClearDetail();
        SetStatus($"{values.Count}개 값 — {TxtPath.Text}");

        // 탭 전환
        if (MainTab.SelectedItem != TabValues)
            MainTab.SelectedItem = TabValues;
    }

    // ── 주소 표시줄 직접 입력 ───────────────────────────────────────
    private void TxtPath_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NavigateTo(TxtPath.Text.Trim());
    }

    private void NavigateTo(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return;
        try
        {
            var hive    = RegistryService.ParseHive(fullPath);
            var subPath = RegistryService.StripHive(fullPath);

            var values = RegistryService.GetValues(hive, subPath);
            DgValues.ItemsSource = values;
            SetStatus($"{values.Count}개 값 — {fullPath}");

            _navHistory.Add(fullPath);
            _navIndex = _navHistory.Count - 1;
        }
        catch (Exception ex)
        {
            SetStatus($"오류: {ex.Message}");
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_navIndex <= 0) return;
        _navIndex--;
        NavigateTo(_navHistory[_navIndex]);
        TxtPath.Text = _navHistory[_navIndex];
    }

    // ── 값 목록 선택 ────────────────────────────────────────────────
    private void DgValues_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (DgValues.SelectedItem is not RegValue val) return;
        _selectedValue = val;
        ShowDetail(val);
    }

    private void DgValues_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selectedValue != null) BtnEditValue_Click(sender, e);
    }

    private void ShowDetail(RegValue val)
    {
        TxtDetailNameV.Text  = string.IsNullOrEmpty(val.Name) ? "(기본값)" : val.Name;
        TxtDetailKindV.Text  = val.KindDisplay;
        TxtDetailDataV.Text  = val.DataDisplay;
    }

    private void ClearDetail()
    {
        TxtDetailNameV.Text  = "";
        TxtDetailKindV.Text  = "";
        TxtDetailDataV.Text  = "";
        _selectedValue       = null;
    }

    // ── 값 편집 ─────────────────────────────────────────────────────
    private void BtnEditValue_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null || _selectedValue == null) return;

        // HKLM 쓰기 보호
        if (!ConfirmHklmWrite()) return;

        var backup = RegistryService.BackupKeyToReg(_selectedNode.Hive, _selectedNode.FullPath);

        var dlg = new ValueEditDialog(_selectedValue);
        dlg.Owner = this;
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_selectedNode.Hive, RegistryView.Default);
            using var key     = baseKey.OpenSubKey(_selectedNode.FullPath, writable: true);
            if (key == null) { SetStatus("키 쓰기 권한 없음"); return; }

            key.SetValue(_selectedValue.Name, dlg.NewValue!, _selectedValue.Kind);
            var values = RegistryService.GetValues(_selectedNode.Hive, _selectedNode.FullPath);
            DgValues.ItemsSource = values;
            SetStatus($"✅ 값 수정 완료 (백업: {Path.GetFileName(backup)})");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"값 수정 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 값 삭제 ─────────────────────────────────────────────────────
    private void BtnDeleteValue_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null || _selectedValue == null) return;
        if (!ConfirmHklmWrite()) return;

        var name = string.IsNullOrEmpty(_selectedValue.Name) ? "(기본값)" : _selectedValue.Name;
        if (MessageBox.Show($"'{name}' 값을 삭제하시겠습니까?", "Reg.Vault",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var backup = RegistryService.BackupKeyToReg(_selectedNode.Hive, _selectedNode.FullPath);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_selectedNode.Hive, RegistryView.Default);
            using var key     = baseKey.OpenSubKey(_selectedNode.FullPath, writable: true);
            if (key == null) { SetStatus("키 쓰기 권한 없음"); return; }

            if (string.IsNullOrEmpty(_selectedValue.Name))
                key.DeleteValue("", throwOnMissingValue: false);
            else
                key.DeleteValue(_selectedValue.Name, throwOnMissingValue: false);

            var values = RegistryService.GetValues(_selectedNode.Hive, _selectedNode.FullPath);
            DgValues.ItemsSource = values;
            ClearDetail();
            SetStatus($"✅ 값 삭제 완료 (백업: {Path.GetFileName(backup)})");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"값 삭제 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 키 삭제 ─────────────────────────────────────────────────────
    private void BtnDeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null || string.IsNullOrEmpty(_selectedNode.FullPath)) return;
        if (!ConfirmHklmWrite()) return;

        if (MessageBox.Show($"키 전체를 삭제하시겠습니까?\n{TxtPath.Text}\n\n⚠ 하위 키도 모두 삭제됩니다.",
            "Reg.Vault", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var backup = RegistryService.BackupKeyToReg(_selectedNode.Hive, _selectedNode.FullPath);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_selectedNode.Hive, RegistryView.Default);
            baseKey.DeleteSubKeyTree(_selectedNode.FullPath, throwOnMissingSubKey: false);
            SetStatus($"✅ 키 삭제 완료 (백업: {Path.GetFileName(backup)})");
            // 트리 새로고침 불필요 — 다음 탐색 시 자동 반영
        }
        catch (Exception ex)
        {
            MessageBox.Show($"키 삭제 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ConfirmHklmWrite()
    {
        if (_selectedNode == null) return true;
        if (_selectedNode.Hive != RegistryHive.LocalMachine) return true;

        return MessageBox.Show(
            "⚠ HKEY_LOCAL_MACHINE 하위 키를 수정하려 합니다.\n" +
            "백업 .reg 파일이 자동 생성됩니다.\n\n계속 진행하시겠습니까?",
            "Reg.Vault — HKLM 수정 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
    }

    // ── 경로 복사 ────────────────────────────────────────────────────
    private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TxtPath.Text);
        SetStatus("경로 복사됨");
    }

    // ── 북마크 ─────────────────────────────────────────────────────
    private void BtnBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtPath.Text)) return;
        var label = TxtPath.Text.Split('\\').Last();
        if (string.IsNullOrEmpty(label)) label = TxtPath.Text;

        var bm = new Bookmark { Label = label, FullPath = TxtPath.Text };
        _bookmarks.Add(bm);
        BookmarkStore.Save(_bookmarks);
        SetStatus($"북마크 추가: {label}");
    }

    private void BtnDeleteBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (LstBookmarks.SelectedItem is not Bookmark bm) return;
        _bookmarks.Remove(bm);
        BookmarkStore.Save(_bookmarks);
    }

    private void LstBookmarks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (LstBookmarks.SelectedItem is not Bookmark bm) return;
        TxtPath.Text = bm.FullPath;
        NavigateTo(bm.FullPath);
    }

    // ── 검색 ────────────────────────────────────────────────────────
    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnSearch_Click(sender, e);
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        var pattern = TxtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(pattern)) return;

        System.Text.RegularExpressions.Regex regex;
        try
        {
            regex = new System.Text.RegularExpressions.Regex(pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            SetStatus("잘못된 정규식 패턴");
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        BtnSearch.IsEnabled       = false;
        BtnSearchCancel.IsEnabled = true;
        PbSearch.Visibility       = Visibility.Visible;
        DgSearchResults.ItemsSource = null;

        MainTab.SelectedItem = TabSearch;

        var searchHive = _selectedNode?.Hive ?? RegistryHive.CurrentUser;
        var searchPath = _selectedNode?.FullPath ?? "";

        // 범위 선택
        var scope = (CmbSearchScope.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (scope == "HKEY_LOCAL_MACHINE")      { searchHive = RegistryHive.LocalMachine;  searchPath = ""; }
        else if (scope == "HKEY_CURRENT_USER")  { searchHive = RegistryHive.CurrentUser;   searchPath = ""; }
        else if (scope == "전체 하이브")
        {
            // 전체 검색: HKLM + HKCU
            var allResults = new List<SearchResult>();
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser, RegistryHive.ClassesRoot })
            {
                if (ct.IsCancellationRequested) break;
                SetStatus($"검색 중: {RegistryService.HiveDisplayName(hive)}...");
                var partial = await RegistryService.SearchAsync(hive, "", regex,
                    ChkSearchKeys.IsChecked == true,
                    ChkSearchValues.IsChecked == true,
                    ChkSearchData.IsChecked == true,
                    32, ct,
                    new Progress<string>(p => SetStatus($"검색 중: {p}")));
                allResults.AddRange(partial);
            }
            DgSearchResults.ItemsSource = allResults;
            SetStatus($"검색 완료 — {allResults.Count}개 결과");
            goto Done;
        }

        var results = await RegistryService.SearchAsync(searchHive, searchPath, regex,
            ChkSearchKeys.IsChecked == true,
            ChkSearchValues.IsChecked == true,
            ChkSearchData.IsChecked == true,
            32, ct,
            new Progress<string>(p => SetStatus($"검색 중: {p}")));

        DgSearchResults.ItemsSource = results;
        SetStatus($"검색 완료 — {results.Count}개 결과");

        Done:
        BtnSearch.IsEnabled       = true;
        BtnSearchCancel.IsEnabled = false;
        PbSearch.Visibility       = Visibility.Collapsed;
    }

    private void BtnSearchCancel_Click(object sender, RoutedEventArgs e)
    {
        _searchCts?.Cancel();
        BtnSearchCancel.IsEnabled = false;
        SetStatus("검색 취소됨");
    }

    private void DgSearchResult_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgSearchResults.SelectedItem is not SearchResult result) return;
        TxtPath.Text = $"{result.HiveName}\\{result.KeyPath}";
        NavigateTo(TxtPath.Text);
        MainTab.SelectedItem = TabValues;
    }

    // ── 내보내기 ────────────────────────────────────────────────────
    private void BtnExportReg_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) { SetStatus("내보낼 키를 선택하세요"); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title  = "REG 파일로 내보내기",
            Filter = "레지스트리 파일 (*.reg)|*.reg",
            FileName = _selectedNode.Name
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            RegistryService.ExportToReg(_selectedNode.Hive, _selectedNode.FullPath, dlg.FileName);
            SetStatus($"✅ REG 내보내기 완료: {dlg.FileName}");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "오류"); }
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) { SetStatus("내보낼 키를 선택하세요"); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title  = "JSON 파일로 내보내기",
            Filter = "JSON 파일 (*.json)|*.json",
            FileName = _selectedNode.Name
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            RegistryService.ExportToJson(_selectedNode.Hive, _selectedNode.FullPath, dlg.FileName);
            SetStatus($"✅ JSON 내보내기 완료: {dlg.FileName}");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "오류"); }
    }

    // ── 스냅샷 ──────────────────────────────────────────────────────
    private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) { SetStatus("스냅샷 찍을 키를 선택하세요"); return; }

        SetStatus("스냅샷 캡처 중...");
        var label = $"{_selectedNode.Name} [{DateTime.Now:MM-dd HH:mm:ss}]";
        var snap  = RegistryService.TakeSnapshot(_selectedNode.Hive, _selectedNode.FullPath, label);
        _snapshots.Add(snap);
        SaveSnapshots();
        SetStatus($"✅ 스냅샷 저장: {label}");
    }

    private void BtnCompareDiff_Click(object sender, RoutedEventArgs e)
    {
        MainTab.SelectedItem = TabDiff;
    }

    private void BtnDiff_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSnapOld.SelectedItem is not RegSnapshot older ||
            CmbSnapNew.SelectedItem is not RegSnapshot newer)
        {
            SetStatus("비교할 스냅샷 2개를 선택하세요");
            return;
        }
        var diffs = RegistryService.CompareSnapshots(older, newer);
        DgDiff.ItemsSource = diffs;
        SetStatus($"비교 완료 — {diffs.Count}개 변경");
    }

    private void BtnDeleteSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = new List<RegSnapshot>();
        if (CmbSnapOld.SelectedItem is RegSnapshot s1) toDelete.Add(s1);
        if (CmbSnapNew.SelectedItem is RegSnapshot s2 && !toDelete.Contains(s2)) toDelete.Add(s2);
        foreach (var s in toDelete) _snapshots.Remove(s);
        SaveSnapshots();
    }

    private void DgDiff_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgDiff.SelectedItem is not DiffEntry diff) return;
        // 스냅샷의 경로는 하이브 프리픽스 없이 저장되므로 HKCU로 추정
        var hive    = RegistryService.ParseHive(diff.KeyPath);
        var subPath = RegistryService.StripHive(diff.KeyPath);
        // 실제로는 스냅샷에 RootPath가 있어 hive를 알 수 있음
        NavigateTo(diff.KeyPath);
        MainTab.SelectedItem = TabValues;
    }

    // ── 스냅샷 저장/로드 ────────────────────────────────────────────
    private static string SnapshotFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RegVault", "snapshots.json");

    private void SaveSnapshots()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotFile)!);
        File.WriteAllText(SnapshotFile, JsonSerializer.Serialize(_snapshots.ToList(),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadSnapshots()
    {
        try
        {
            if (!File.Exists(SnapshotFile)) return;
            var list = JsonSerializer.Deserialize<List<RegSnapshot>>(File.ReadAllText(SnapshotFile));
            if (list == null) return;
            foreach (var s in list) _snapshots.Add(s);
        }
        catch { }
    }

    // ── 탭 전환 ─────────────────────────────────────────────────────
    private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 필요 시 사용
    }

    // ── 상태 ─────────────────────────────────────────────────────────
    private void SetStatus(string msg) =>
        Dispatcher.Invoke(() => TxtStatus.Text = msg);
}
