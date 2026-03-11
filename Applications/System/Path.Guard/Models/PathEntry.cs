namespace PathGuard.Models;

public enum PathScope { System, User }
public enum PathStatus { Ok, Broken, Duplicate, Disabled }

public class PathEntry : INotifyPropertyChanged
{
    private string _rawValue = "";
    private string _expandedValue = "";
    private PathScope _scope;
    private PathStatus _status = PathStatus.Ok;
    private bool _isEnabled = true;
    private bool _isSelected;

    public string RawValue      { get => _rawValue;      set => Set(ref _rawValue, value); }
    public string ExpandedValue { get => _expandedValue; set => Set(ref _expandedValue, value); }
    public PathScope Scope      { get => _scope;         set => Set(ref _scope, value); }
    public PathStatus Status    { get => _status;        set { Set(ref _status, value); OnPropChanged(nameof(StatusBrush)); OnPropChanged(nameof(StatusText)); } }
    public bool IsEnabled       { get => _isEnabled;     set { Set(ref _isEnabled, value); OnPropChanged(nameof(StatusBrush)); } }
    public bool IsSelected      { get => _isSelected;    set => Set(ref _isSelected, value); }

    /// <summary>히트된 실행파일 목록 (검색 결과)</summary>
    public List<string> HitFiles { get; set; } = [];
    public bool HasHits => HitFiles.Count > 0;

    public Brush StatusBrush
    {
        get
        {
            if (!_isEnabled) return new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            return _status switch
            {
                PathStatus.Ok        => new SolidColorBrush(Color.FromRgb(0x6E, 0xFF, 0x6E)),
                PathStatus.Broken    => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)),
                PathStatus.Duplicate => new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                PathStatus.Disabled  => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                _ => Brushes.White
            };
        }
    }

    public string StatusText => !_isEnabled ? "비활성" : _status switch
    {
        PathStatus.Ok        => "정상",
        PathStatus.Broken    => "경로 없음",
        PathStatus.Duplicate => "중복",
        PathStatus.Disabled  => "비활성",
        _ => ""
    };

    public string ScopeLabel => _scope == PathScope.System ? "시스템" : "사용자";
    public Brush  ScopeBrush => _scope == PathScope.System
        ? new SolidColorBrush(Color.FromRgb(0x7B, 0x68, 0xEE))
        : new SolidColorBrush(Color.FromRgb(0x5B, 0xB8, 0xFF));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T f, T v, [System.Runtime.CompilerServices.CallerMemberName] string? n = null)
    { if (Equals(f, v)) return; f = v; OnPropChanged(n); }
    private void OnPropChanged(string? n) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
