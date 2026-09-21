using AIPForge.Models;
using AIPForge.Views;

namespace AIPForge;

/// <summary>전체 검색 · XML 원문 편집</summary>
public partial class MainWindow
{
    private const int MaxSearchHits = 5000;

    public sealed record SearchHit(string Table, string Key, string Attribute, string Value, AipElement Component, AipElement Row);

    private bool _rawDirty;
    private bool _rawLoading;

    // ── 전체 검색 ───────────────────────────────────────────────────

    private void CmdFind_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        TxtGlobalSearch.Focus();
        TxtGlobalSearch.SelectAll();
    }

    private void TxtGlobalSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) RunGlobalSearch(TxtGlobalSearch.Text.Trim());
        else if (e.Key == Key.Escape) TxtGlobalSearch.Text = "";
    }

    private void RunGlobalSearch(string query)
    {
        if (_doc == null || query.Length == 0) return;
        if (!CommitPendingEdits()) return;

        var hits = new List<SearchHit>();
        foreach (var comp in _doc.Components)
        {
            var title = ComponentCatalog.Get(AipDocument.ShortCid(comp)).Title;
            foreach (var row in AipDocument.Rows(comp))
            {
                var key = row.Attributes.FirstOrDefault()?.Value ?? "";
                foreach (var a in row.Attributes)
                {
                    if (!a.Value.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    hits.Add(new SearchHit(title, key, a.Name, a.Value, comp, row));
                    if (hits.Count >= MaxSearchHits) break;
                }
                if (hits.Count >= MaxSearchHits) break;
            }
            if (hits.Count >= MaxSearchHits) break;
        }

        GridSearch.ItemsSource = hits;
        TxtSearchTitle.Text = $"'{query}' 검색 결과 — {hits.Count:N0}건" + (hits.Count >= MaxSearchHits ? " (최대 개수에서 멈춤)" : "");

        if (_searchNavItem == null)
        {
            _searchNavItem = MakeNavItem("검색 결과", null, new NavTarget(NavKind.Search), bold: true);
            NavTree.Items.Insert(_overviewNavItem != null ? NavTree.Items.IndexOf(_overviewNavItem) + 1 : 0, _searchNavItem);
        }
        SelectNav(_searchNavItem);
        SetStatus($"'{query}' — {hits.Count:N0}건 찾음");
    }

    private void GridSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridSearch.SelectedItem is SearchHit hit) NavigateToCell(hit.Component, hit.Row, hit.Attribute);
    }

    // ── XML 원문 ────────────────────────────────────────────────────

    private void LoadRaw()
    {
        if (_doc == null) return;
        _rawLoading = true;
        TxtRaw.Text = _doc.Serialize();
        _rawLoading = false;
        _rawDirty = false;
        TxtRawStatus.Text = $"{TxtRaw.LineCount:N0}줄";
        BtnRawApply.IsEnabled = false;
    }

    private void TxtRaw_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_rawLoading) return;
        _rawDirty = true;
        BtnRawApply.IsEnabled = true;
        TxtRawStatus.Text = "편집 중 — '편집 내용 적용'을 누르면 반영됩니다";
    }

    private void BtnRawApply_Click(object sender, RoutedEventArgs e)
    {
        if (TryApplyRaw(showError: true)) SetStatus("XML 편집 내용 적용됨 — 저장해야 파일에 반영됩니다.");
    }

    private void BtnRawRevert_Click(object sender, RoutedEventArgs e) => LoadRaw();

    /// <summary>원문을 다시 해석해 문서를 교체. 오류면 false</summary>
    private bool TryApplyRaw(bool showError)
    {
        if (_doc == null || !_rawDirty) return true;
        var before = _doc.Serialize();
        if (TxtRaw.Text == before)
        {
            _rawDirty = false;
            BtnRawApply.IsEnabled = false;
            return true;
        }
        try
        {
            _doc.ReplaceContent(TxtRaw.Text);
        }
        catch (Exception ex)
        {
            TxtRawStatus.Text = "XML 오류: " + ex.Message;
            if (showError) DarkDialog.Info(this, $"XML을 해석할 수 없습니다. 오류를 고친 뒤 다시 적용하세요.\n\n{ex.Message}");
            return false;
        }

        _rawDirty = false;
        _tables.Clear();
        _table = null;
        MarkDirty();

        // 컴포넌트 객체가 새로 만들어졌으므로 트리를 다시 구성하고 원문 화면 유지
        var wasRaw = _current?.Kind == NavKind.Raw;
        BuildNav();
        PopulateBuildCombo();
        RefreshCurrentFolderItem();
        if (wasRaw)
        {
            _suppressNav = true;
            if (_rawNavItem != null) _rawNavItem.IsSelected = true;
            _suppressNav = false;
            _current = new NavTarget(NavKind.Raw);
            LoadRaw();
        }
        return true;
    }

    private void TxtRawFind_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var q = TxtRawFind.Text;
        if (q.Length == 0) return;
        var text = TxtRaw.Text;
        var backward = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        int idx;
        if (backward)
        {
            var from = TxtRaw.SelectionStart - 1;
            idx = from < 0 ? -1 : text.LastIndexOf(q, from, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = text.LastIndexOf(q, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var from = TxtRaw.SelectionStart + TxtRaw.SelectionLength;
            idx = text.IndexOf(q, Math.Min(from, text.Length), StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        }

        if (idx < 0)
        {
            TxtRawStatus.Text = "찾지 못함";
            return;
        }
        TxtRaw.Select(idx, q.Length);
        var line = TxtRaw.GetLineIndexFromCharacterIndex(idx);
        TxtRaw.ScrollToLine(line);
        TxtRawStatus.Text = $"{line + 1:N0}번째 줄";
        e.Handled = true;
    }
}
