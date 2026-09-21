using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;
using AIPForge.Models;
using AIPForge.Views;

namespace AIPForge;

/// <summary>컴포넌트 테이블 표 편집</summary>
public partial class MainWindow
{
    private readonly Dictionary<AipElement, ComponentTable> _tables = [];
    private ComponentTable? _table;
    private bool _committingRow;

    private void ShowTable(AipElement component)
    {
        if (!_tables.TryGetValue(component, out var table))
        {
            table = new ComponentTable(component);
            table.Changed += () => OnTableChanged(table);
            _tables[component] = table;
        }
        _table = table;

        var meta = ComponentCatalog.Get(AipDocument.ShortCid(component));
        TxtTableTitle.Text = meta.Title;
        TxtTableDesc.Text = $"{meta.Description}   ·   {component.Get("cid")}";

        TableGrid.ItemsSource = null;
        TableGrid.Columns.Clear();
        for (var i = 0; i < table.Columns.Count; i++) TableGrid.Columns.Add(MakeColumn(table.Columns[i], i == 0));
        TableGrid.FrozenColumnCount = table.Columns.Count > 1 ? 1 : 0;

        TxtTableFilter.Text = "";
        table.Table.DefaultView.RowFilter = "";
        table.Table.DefaultView.Sort = "";
        TableGrid.ItemsSource = table.Table.DefaultView;

        var extras = table.ExtraAttributes.Select(a => $"{a.Get("name")} = {a.Get("value")}").ToList();
        TxtTableExtra.Text = extras.Count > 0 ? "테이블 설정: " + string.Join("   ·   ", extras) : "";
        TxtTableExtra.Visibility = extras.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateTableCount();
        PnlTable.Visibility = Visibility.Visible;
    }

    private DataGridTextColumn MakeColumn(string name, bool isKey) => new()
    {
        Header = name,
        Binding = new Binding($"[{name}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
        ElementStyle = (Style)FindResource(isKey ? "CellKeyText" : "CellText"),
        EditingElementStyle = (Style)FindResource("CellEditBox"),
        MinWidth = 60,
        MaxWidth = 460,
    };

    private void OnTableChanged(ComponentTable table)
    {
        MarkDirty();
        UpdateNavCount(table.Component);
        if (ReferenceEquals(table, _table)) UpdateTableCount();
    }

    private void UpdateTableCount()
    {
        if (_table == null) return;
        var total = _table.Table.Rows.Count;
        var shown = _table.Table.DefaultView.Count;
        TxtTableCount.Text = shown == total ? $"{total:N0}행 · {_table.Columns.Count}열" : $"{shown:N0} / {total:N0}행 표시 · {_table.Columns.Count}열";
    }

    /// <summary>표 외부(개요·버전 변경·원문 적용)에서 문서가 바뀌면 캐시된 표를 버리고 다시 그림</summary>
    private void InvalidateTables()
    {
        _tables.Clear();
        _table = null;
        if (_current?.Kind == NavKind.Component && _current.Component != null) ShowTable(_current.Component);
    }

    // ── 셀 편집 즉시 반영 ───────────────────────────────────────────

    private void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || _committingRow) return;
        // 셀 하나를 고칠 때마다 행을 커밋해 문서에 바로 반영 (수정됨 표시·저장이 즉시 동작)
        Dispatcher.BeginInvoke(() =>
        {
            _committingRow = true;
            try { TableGrid.CommitEdit(DataGridEditingUnit.Row, true); }
            finally { _committingRow = false; }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && !IsEditingCell())
        {
            e.Handled = true;
            DeleteSelectedRows();
        }
    }

    private bool IsEditingCell()
        => Keyboard.FocusedElement is TextBox tb && tb.IsDescendantOf(TableGrid);

    // ── 필터 ────────────────────────────────────────────────────────

    private void TxtTableFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtFilterHint.Visibility = TxtTableFilter.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_table == null) return;
        var q = TxtTableFilter.Text.Trim();
        try
        {
            _table.Table.DefaultView.RowFilter = q.Length == 0
                ? ""
                : string.Join(" OR ", _table.Columns.Select(c => $"[{c.Replace("]", "\\]")}] LIKE '%{EscapeLike(q)}%'"));
        }
        catch (Exception)
        {
            _table.Table.DefaultView.RowFilter = "";
        }
        UpdateTableCount();
    }

    /// <summary>DataView LIKE 패턴 이스케이프 — 와일드카드·대괄호는 [ ]로 감싸고 작은따옴표는 두 번</summary>
    private static string EscapeLike(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            if (ch is '[' or ']' or '*' or '%') sb.Append('[').Append(ch).Append(']');
            else if (ch == '\'') sb.Append("''");
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    // ── 행 · 열 조작 ────────────────────────────────────────────────

    private List<DataRow> SelectedRows()
    {
        var rows = TableGrid.SelectedCells
            .Select(c => c.Item)
            .OfType<DataRowView>()
            .Select(v => v.Row)
            .Distinct()
            .ToList();
        if (rows.Count == 0 && TableGrid.CurrentItem is DataRowView cur) rows.Add(cur.Row);
        return rows;
    }

    private void BtnRowAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_table == null) return;
        TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        TxtTableFilter.Text = "";
        var row = _table.AddRow(null);
        FocusRow(row, _table.KeyColumn, beginEdit: true);
        SetStatus($"행 추가됨 — 키: {row[_table.KeyColumn]}");
    }

    private void BtnRowDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_table == null) return;
        TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var rows = SelectedRows().OrderBy(r => _table.Table.Rows.IndexOf(r)).ToList();
        if (rows.Count == 0)
        {
            SetStatus("복제할 행(셀)을 먼저 선택하세요.");
            return;
        }
        DataRow? last = null;
        foreach (var r in rows) last = _table.AddRow(r);
        if (last != null) FocusRow(last, _table.KeyColumn, beginEdit: false);
        SetStatus($"{rows.Count}행 복제됨 — 키 값은 자동으로 고유하게 바뀌었습니다. 필요하면 수정하세요.");
    }

    private void BtnRowDelete_Click(object sender, RoutedEventArgs e) => DeleteSelectedRows();

    private void DeleteSelectedRows()
    {
        if (_table == null) return;
        TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var rows = SelectedRows();
        if (rows.Count == 0)
        {
            SetStatus("삭제할 행(셀)을 먼저 선택하세요.");
            return;
        }
        var keys = rows.Take(6).Select(r => "  · " + (r[_table.KeyColumn] as string ?? "?"));
        var more = rows.Count > 6 ? $"\n  … 외 {rows.Count - 6}개" : "";
        if (!DarkDialog.Confirm(this,
                $"{rows.Count}개 행을 삭제할까요?\n\n{string.Join("\n", keys)}{more}\n\n다른 테이블에서 이 키를 참조하고 있다면 함께 정리해야 빌드 오류가 나지 않습니다.",
                "삭제"))
            return;
        _table.DeleteRows(rows);
        SetStatus($"{rows.Count}행 삭제됨");
    }

    private void BtnColumnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_table == null || _current?.Component == null) return;
        var name = InputDialog.Ask(this, "추가할 속성(열) 이름 — 예: Description", "", v =>
            !Regex.IsMatch(v, "^[A-Za-z_][A-Za-z0-9_.]*$") ? "영문자·숫자·밑줄만 쓸 수 있고 숫자로 시작할 수 없습니다."
            : _table.Columns.Contains(v) ? "이미 있는 열입니다."
            : null);
        if (name == null || !_table.AddColumn(name)) return;
        TableGrid.Columns.Add(MakeColumn(name, false));
        SetStatus($"'{name}' 열 추가됨 — 값을 입력한 행에만 속성이 기록됩니다.");
    }

    private void BtnCellClear_Click(object sender, RoutedEventArgs e)
    {
        if (_table == null) return;
        TableGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var count = 0;
        foreach (var cell in TableGrid.SelectedCells.ToList())
        {
            if (cell.Item is not DataRowView v || cell.Column?.Header is not string col) continue;
            if (col == _table.KeyColumn) continue; // 키 속성은 제거 불가
            if (v.Row[col] is DBNull) continue;
            _table.ClearCell(v.Row, col);
            count++;
        }
        SetStatus(count > 0 ? $"속성 {count}개 제거됨" : "제거할 속성이 없습니다. (키 열은 제거할 수 없습니다)");
    }

    private void MnuCopyCell_Click(object sender, RoutedEventArgs e)
    {
        var cell = TableGrid.CurrentCell;
        if (cell.Item is DataRowView v && cell.Column?.Header is string col && v.Row[col] is string s)
        {
            Clipboard.SetText(s);
            SetStatus("복사됨: " + (s.Length > 80 ? s[..80] + "…" : s));
        }
    }

    /// <summary>현재 행의 SourcePath(원본 파일) 위치를 탐색기로 열기</summary>
    private void MnuOpenSource_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null || TableGrid.CurrentItem is not DataRowView v) return;
        foreach (var col in new[] { "SourcePath", "Path", "SourceFolder" })
        {
            if (!v.Row.Table.Columns.Contains(col) || v.Row[col] is not string rel) continue;
            var full = _doc.ResolvePath(_doc.ExpandFormatted(rel));
            if (full == null) continue;
            if (File.Exists(full) || System.IO.Directory.Exists(full)) OpenInExplorer(full);
            else SetStatus($"없는 경로: {full}");
            return;
        }
        SetStatus("이 행에는 원본 경로(SourcePath) 속성이 없습니다.");
    }

    private void FocusRow(DataRow row, string column, bool beginEdit)
    {
        var view = _table?.Table.DefaultView;
        if (view == null) return;
        DataRowView? target = null;
        foreach (DataRowView v in view)
            if (ReferenceEquals(v.Row, row)) { target = v; break; }
        var col = TableGrid.Columns.FirstOrDefault(c => c.Header as string == column) ?? TableGrid.Columns.FirstOrDefault();
        if (target == null || col == null) return;

        TableGrid.UpdateLayout();
        TableGrid.ScrollIntoView(target, col);
        TableGrid.SelectedCells.Clear();
        var info = new DataGridCellInfo(target, col);
        TableGrid.CurrentCell = info;
        TableGrid.SelectedCells.Add(info);
        TableGrid.Focus();
        if (beginEdit) Dispatcher.BeginInvoke(() => TableGrid.BeginEdit(), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>검색 결과 등에서 특정 테이블의 셀로 이동</summary>
    private void NavigateToCell(AipElement component, AipElement row, string attribute)
    {
        if (!_navItems.TryGetValue(component, out var item)) return;
        SelectNav(item);
        Dispatcher.BeginInvoke(() =>
        {
            if (_table == null || !ReferenceEquals(_table.Component, component)) return;
            var dr = _table.RowOf(row);
            if (dr != null) FocusRow(dr, attribute, beginEdit: false);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
