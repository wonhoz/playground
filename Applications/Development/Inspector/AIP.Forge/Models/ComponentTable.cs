using System.Data;

namespace AIPForge.Models;

/// <summary>
/// COMPONENT의 ROW 목록을 DataGrid용 DataTable로 펼치고, 편집 결과를 원본 요소에 되돌려 쓴다.
/// 셀 값 DBNull = 속성 없음, "" = 빈 값 속성.
/// </summary>
public sealed class ComponentTable
{
    public AipElement Component { get; }
    public DataTable Table { get; } = new();
    public List<string> Columns { get; } = [];
    public string KeyColumn => Columns.Count > 0 ? Columns[0] : "";

    /// <summary>ROW가 아닌 자식 (컴포넌트 수준 ATTRIBUTE)</summary>
    public IEnumerable<AipElement> ExtraAttributes => Component.Children.Where(c => c.Name == "ATTRIBUTE");

    public event Action? Changed;

    private readonly Dictionary<DataRow, AipElement> _rowMap = [];
    private bool _suspend;

    public ComponentTable(AipElement component)
    {
        Component = component;
        var rows = AipDocument.Rows(component).ToList();

        // 행마다 속성 구성이 달라도 AI 스키마 순서를 유지하도록,
        // 처음 보는 속성은 같은 행에서 바로 앞에 있던 속성 뒤에 끼워 넣는다
        foreach (var r in rows)
        {
            string? prev = null;
            foreach (var a in r.Attributes)
            {
                if (!Columns.Contains(a.Name))
                {
                    var at = prev == null ? 0 : Columns.IndexOf(prev) + 1;
                    Columns.Insert(at, a.Name);
                }
                prev = a.Name;
            }
        }

        foreach (var c in Columns) Table.Columns.Add(c, typeof(string));

        foreach (var r in rows)
        {
            var dr = Table.NewRow();
            foreach (var a in r.Attributes) dr[a.Name] = a.Value;
            Table.Rows.Add(dr);
            _rowMap[dr] = r;
        }
        Table.AcceptChanges();
        Table.RowChanged += OnRowChanged;
    }

    public AipElement? ElementOf(DataRow row) => _rowMap.GetValueOrDefault(row);

    public DataRow? RowOf(AipElement element)
    {
        foreach (var kv in _rowMap)
            if (ReferenceEquals(kv.Value, element)) return kv.Key;
        return null;
    }

    private void OnRowChanged(object sender, DataRowChangeEventArgs e)
    {
        if (_suspend || e.Action != DataRowAction.Change) return;
        SyncRow(e.Row);
    }

    private void SyncRow(DataRow row)
    {
        if (!_rowMap.TryGetValue(row, out var el)) return;
        var changed = false;
        foreach (var col in Columns)
        {
            var value = row[col];
            if (value is DBNull)
            {
                changed |= el.Remove(col);
                continue;
            }

            var text = (string)value;
            // 없던 속성 칸에 들어갔다 그냥 나온 경우("")는 속성 추가로 보지 않는다
            if (text.Length == 0 && !el.Has(col))
            {
                WithoutEvents(() => row[col] = DBNull.Value);
                continue;
            }
            if (el.Get(col) != text)
            {
                el.Set(col, text, Columns);
                changed = true;
            }
        }
        if (changed) Changed?.Invoke();
    }

    /// <summary>셀을 "속성 없음"으로 되돌림</summary>
    public void ClearCell(DataRow row, string column)
    {
        if (!_rowMap.TryGetValue(row, out var el) || !el.Has(column)) return;
        WithoutEvents(() => row[column] = DBNull.Value);
        el.Remove(column);
        Changed?.Invoke();
    }

    /// <summary>새 행 추가 — <paramref name="template"/>이 있으면 복제 후 바로 아래에 삽입</summary>
    public DataRow AddRow(DataRow? template)
    {
        AipElement el;
        int insertAfterIndex;
        if (template != null && _rowMap.TryGetValue(template, out var src))
        {
            el = src.Clone();
            insertAfterIndex = Component.Children.IndexOf(src);
        }
        else
        {
            el = new AipElement("ROW");
            insertAfterIndex = Component.Children.FindLastIndex(c => c.Name == "ROW");
        }

        if (KeyColumn.Length > 0)
            el.Set(KeyColumn, UniqueKey(el.Get(KeyColumn) ?? "NewRow"), Columns);

        Component.AddChild(el, insertAfterIndex + 1);

        var dr = Table.NewRow();
        foreach (var a in el.Attributes) dr[a.Name] = a.Value;
        WithoutEvents(() =>
        {
            var tableIndex = template != null ? Table.Rows.IndexOf(template) + 1 : Table.Rows.Count;
            Table.Rows.InsertAt(dr, tableIndex);
            Table.AcceptChanges();
        });
        _rowMap[dr] = el;
        Changed?.Invoke();
        return dr;
    }

    public void DeleteRows(IEnumerable<DataRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        WithoutEvents(() =>
        {
            foreach (var dr in list)
            {
                if (_rowMap.Remove(dr, out var el)) Component.Children.Remove(el);
                Table.Rows.Remove(dr);
            }
            Table.AcceptChanges();
        });
        Changed?.Invoke();
    }

    public bool AddColumn(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Columns.Contains(name)) return false;
        Columns.Add(name);
        WithoutEvents(() => Table.Columns.Add(name, typeof(string)));
        return true;
    }

    private string UniqueKey(string baseKey)
    {
        var existing = new HashSet<string>(_rowMap.Values.Select(v => v.Get(KeyColumn) ?? ""), StringComparer.Ordinal);
        if (!existing.Contains(baseKey)) return baseKey;
        var stem = baseKey;
        var us = baseKey.LastIndexOf('_');
        if (us > 0 && int.TryParse(baseKey[(us + 1)..], out _)) stem = baseKey[..us];
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}_{i}";
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    private void WithoutEvents(Action action)
    {
        _suspend = true;
        try { action(); }
        finally { _suspend = false; }
    }
}
