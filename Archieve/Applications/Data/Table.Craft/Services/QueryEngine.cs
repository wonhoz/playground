using System.Text.RegularExpressions;
using TableCraft.Models;

namespace TableCraft.Services;

/// <summary>인메모리 필터·정렬·집계·피벗 쿼리 엔진</summary>
public class QueryEngine
{
    private string[]   _headers = [];
    private string[][] _rows    = [];
    private ColumnType[] _types = [];

    private readonly List<FilterCondition> _filters   = [];
    private readonly List<(int Col, SortState Dir)>  _sortKeys = [];
    private readonly Dictionary<int, string>          _quickFilters = [];

    public bool HasData => _rows.Length > 0;
    public int  TotalRows    => _rows.Length;

    public void Load(string[] headers, string[][] rows, ColumnType[] types)
    {
        _headers = headers;
        _rows    = rows;
        _types   = types;
        _filters.Clear();
        _sortKeys.Clear();
        _quickFilters.Clear();
    }

    // ── 빠른 필터 (헤더 TextBox) ──────────────────────────────────────
    public void SetQuickFilter(int colIndex, string text)
    {
        if (string.IsNullOrEmpty(text))
            _quickFilters.Remove(colIndex);
        else
            _quickFilters[colIndex] = text;
    }

    // ── 상세 필터 ─────────────────────────────────────────────────────
    public void AddFilter(FilterCondition f)    => _filters.Add(f);
    public void RemoveFilter(FilterCondition f) => _filters.Remove(f);
    public void ClearFilters()                  { _filters.Clear(); _quickFilters.Clear(); }
    public IReadOnlyList<FilterCondition> Filters => _filters;

    // ── 정렬 ─────────────────────────────────────────────────────────
    public void SetSort(int colIndex, SortState dir, bool additive = false)
    {
        if (!additive) _sortKeys.Clear();
        _sortKeys.RemoveAll(k => k.Col == colIndex);
        if (dir != SortState.None) _sortKeys.Add((colIndex, dir));
    }
    public void ClearSort() => _sortKeys.Clear();

    // ── 필터+정렬 적용 → 인덱스 배열 반환 ───────────────────────────
    public int[] Compute(CancellationToken ct = default)
    {
        var indices = new List<int>(_rows.Length);
        bool hasFilters = _filters.Count > 0 || _quickFilters.Count > 0;

        for (int i = 0; i < _rows.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!hasFilters || MatchesAll(i))
                indices.Add(i);
        }

        if (_sortKeys.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            indices.Sort((a, b) =>
            {
                foreach (var (col, dir) in _sortKeys)
                {
                    int cmp = CompareTyped(_rows[a].ElementAtOrDefault(col) ?? "",
                                          _rows[b].ElementAtOrDefault(col) ?? "", col);
                    if (cmp != 0) return dir == SortState.Asc ? cmp : -cmp;
                }
                return 0;
            });
        }

        return indices.ToArray();
    }

    // ── 집계 ─────────────────────────────────────────────────────────
    public AggregateResult Aggregate(int colIndex, int[] indices)
    {
        var name    = colIndex < _headers.Length ? _headers[colIndex] : $"Col{colIndex}";
        var type    = colIndex < _types.Length   ? _types[colIndex]   : ColumnType.Text;
        bool isNum  = type is ColumnType.Integer or ColumnType.Float;

        long   count    = indices.Length;
        long   empty    = 0;
        double sum      = 0;
        string? min = null, max = null;
        var    uniq     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var i in indices)
        {
            var val = _rows[i].ElementAtOrDefault(colIndex) ?? "";
            if (val.Length == 0) { empty++; continue; }
            uniq.Add(val);

            if (isNum && double.TryParse(val, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                sum += d;
                if (min is null || CompareTyped(val, min, colIndex) < 0) min = val;
                if (max is null || CompareTyped(val, max, colIndex) > 0) max = val;
            }
            else
            {
                if (min is null || string.Compare(val, min, StringComparison.Ordinal) < 0) min = val;
                if (max is null || string.Compare(val, max, StringComparison.Ordinal) > 0) max = val;
            }
        }

        long nonEmpty = count - empty;
        return new AggregateResult
        {
            ColumnName = name,
            Count      = count,
            Distinct   = uniq.Count,
            Empty      = empty,
            Sum        = sum,
            Avg        = nonEmpty > 0 ? sum / nonEmpty : 0,
            Min        = min ?? "",
            Max        = max ?? "",
            IsNumeric  = isNum
        };
    }

    // ── 피벗 ─────────────────────────────────────────────────────────
    /// <summary>피벗 결과: (rowKeys, colKeys, matrix[rowIdx][colIdx])</summary>
    public (string[] RowKeys, string[] ColKeys, string[,] Matrix)
        BuildPivot(PivotConfig cfg, int[] indices)
    {
        if (cfg.RowColumnIndex < 0 || cfg.ColColumnIndex < 0)
            return ([], [], new string[0, 0]);

        var rowSet = new List<string>();
        var colSet = new List<string>();
        var cells  = new Dictionary<(string R, string C), List<double>>();

        foreach (var i in indices)
        {
            var r   = _rows[i].ElementAtOrDefault(cfg.RowColumnIndex) ?? "";
            var c   = _rows[i].ElementAtOrDefault(cfg.ColColumnIndex) ?? "";
            var val = cfg.ValColumnIndex >= 0
                          ? _rows[i].ElementAtOrDefault(cfg.ValColumnIndex) ?? ""
                          : "1";

            if (!rowSet.Contains(r)) rowSet.Add(r);
            if (!colSet.Contains(c)) colSet.Add(c);

            var key = (r, c);
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = [];

            if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                list.Add(d);
            else
                list.Add(1);
        }

        // 정렬
        rowSet.Sort(StringComparer.OrdinalIgnoreCase);
        colSet.Sort(StringComparer.OrdinalIgnoreCase);

        var matrix = new string[rowSet.Count, colSet.Count];
        for (int r = 0; r < rowSet.Count; r++)
        for (int c = 0; c < colSet.Count; c++)
        {
            if (!cells.TryGetValue((rowSet[r], colSet[c]), out var vals) || vals.Count == 0)
            {
                matrix[r, c] = "";
                continue;
            }

            matrix[r, c] = cfg.Aggregation switch
            {
                PivotAgg.Count => vals.Count.ToString(),
                PivotAgg.Sum   => vals.Sum().ToString("G6"),
                PivotAgg.Avg   => (vals.Sum() / vals.Count).ToString("G4"),
                PivotAgg.Min   => vals.Min().ToString("G6"),
                PivotAgg.Max   => vals.Max().ToString("G6"),
                _              => vals.Count.ToString()
            };
        }

        return (rowSet.ToArray(), colSet.ToArray(), matrix);
    }

    // ── 계산 컬럼 평가 ────────────────────────────────────────────────
    public string EvalExpression(string expr, string[] row)
    {
        try { return ExpressionEvaluator.Evaluate(expr, _headers, row); }
        catch { return "#ERROR"; }
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────
    private bool MatchesAll(int rowIdx)
    {
        // 빠른 필터
        foreach (var (col, text) in _quickFilters)
        {
            var cell = _rows[rowIdx].ElementAtOrDefault(col) ?? "";
            if (!cell.Contains(text, StringComparison.OrdinalIgnoreCase)) return false;
        }

        // 상세 필터
        foreach (var f in _filters)
        {
            var cell = _rows[rowIdx].ElementAtOrDefault(f.ColumnIndex) ?? "";
            if (!MatchFilter(cell, f)) return false;
        }

        return true;
    }

    private bool MatchFilter(string cell, FilterCondition f)
    {
        var cmp = f.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return f.Operator switch
        {
            FilterOperator.Contains    => cell.Contains(f.Value, cmp),
            FilterOperator.NotContains => !cell.Contains(f.Value, cmp),
            FilterOperator.Equals      => cell.Equals(f.Value, cmp),
            FilterOperator.NotEquals   => !cell.Equals(f.Value, cmp),
            FilterOperator.StartsWith  => cell.StartsWith(f.Value, cmp),
            FilterOperator.EndsWith    => cell.EndsWith(f.Value, cmp),
            FilterOperator.GreaterThan => CompareTyped(cell, f.Value, f.ColumnIndex) > 0,
            FilterOperator.LessThan    => CompareTyped(cell, f.Value, f.ColumnIndex) < 0,
            FilterOperator.Between     => CompareTyped(cell, f.Value, f.ColumnIndex) >= 0
                                       && CompareTyped(cell, f.Value2, f.ColumnIndex) <= 0,
            FilterOperator.IsEmpty     => cell.Length == 0,
            FilterOperator.IsNotEmpty  => cell.Length > 0,
            FilterOperator.Regex       => Regex.IsMatch(cell, f.Value,
                                              f.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase),
            _                          => true
        };
    }

    private int CompareTyped(string a, string b, int colIndex)
    {
        var type = colIndex < _types.Length ? _types[colIndex] : ColumnType.Text;
        if (type is ColumnType.Integer or ColumnType.Float)
        {
            var nc = System.Globalization.NumberStyles.Any;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (double.TryParse(a, nc, ic, out var da) && double.TryParse(b, nc, ic, out var db))
                return da.CompareTo(db);
        }
        if (type == ColumnType.Date)
        {
            if (DateTime.TryParse(a, out var da) && DateTime.TryParse(b, out var db))
                return da.CompareTo(db);
        }
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
