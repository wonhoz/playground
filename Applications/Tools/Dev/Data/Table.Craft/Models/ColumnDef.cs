namespace TableCraft.Models;

public enum ColumnType { Text, Integer, Float, Date, Boolean }

/// <summary>CSV 컬럼 정의 — DataGrid 헤더 바인딩 모델</summary>
public class ColumnDef : System.ComponentModel.INotifyPropertyChanged
{
    public int        Index { get; init; }
    public string     Name  { get; init; } = "";
    public ColumnType Type  { get; init; }

    public string TypeLabel => Type switch
    {
        ColumnType.Integer => "#",
        ColumnType.Float   => "~",
        ColumnType.Date    => "d",
        ColumnType.Boolean => "✓",
        _                  => "A"
    };

    // ── 빠른 필터 ─────────────────────────────────────────────────────
    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged(nameof(FilterText));
            FilterChanged?.Invoke(Index, value);
        }
    }

    // ── 정렬 표시 ─────────────────────────────────────────────────────
    private SortState _sort = SortState.None;
    public SortState Sort
    {
        get => _sort;
        set { _sort = value; OnPropertyChanged(nameof(Sort)); OnPropertyChanged(nameof(SortIcon)); }
    }
    public string SortIcon => Sort switch
    {
        SortState.Asc  => " ↑",
        SortState.Desc => " ↓",
        _              => ""
    };

    public event Action<int, string>? FilterChanged;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public enum SortState { None, Asc, Desc }
