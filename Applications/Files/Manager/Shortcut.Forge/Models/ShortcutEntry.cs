namespace ShortcutForge.Models;

public enum ShortcutStatus { Ok, BrokenTarget, BrokenIcon, Missing }

public class ShortcutEntry : INotifyPropertyChanged
{
    private string _name = "";
    private string _lnkPath = "";
    private string _targetPath = "";
    private string _arguments = "";
    private string _workingDir = "";
    private string _description = "";
    private string _iconPath = "";
    private int _iconIndex;
    private ShortcutStatus _status = ShortcutStatus.Ok;
    private ImageSource? _iconImage;

    public string Name         { get => _name;        set => Set(ref _name, value); }
    public string LnkPath      { get => _lnkPath;     set => Set(ref _lnkPath, value); }
    public string TargetPath   { get => _targetPath;  set => Set(ref _targetPath, value); }
    public string Arguments    { get => _arguments;   set => Set(ref _arguments, value); }
    public string WorkingDir   { get => _workingDir;  set => Set(ref _workingDir, value); }
    public string Description  { get => _description; set => Set(ref _description, value); }
    public string IconPath     { get => _iconPath;    set => Set(ref _iconPath, value); }
    public int    IconIndex    { get => _iconIndex;   set => Set(ref _iconIndex, value); }
    public ShortcutStatus Status { get => _status;   set { Set(ref _status, value); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(StatusText)); } }
    public ImageSource? IconImage { get => _iconImage; set => Set(ref _iconImage, value); }

    public Brush StatusBrush => _status switch
    {
        ShortcutStatus.Ok          => Brushes.LimeGreen,
        ShortcutStatus.BrokenTarget=> Brushes.OrangeRed,
        ShortcutStatus.BrokenIcon  => Brushes.Yellow,
        ShortcutStatus.Missing     => Brushes.Gray,
        _ => Brushes.White
    };

    public string StatusText => _status switch
    {
        ShortcutStatus.Ok           => "정상",
        ShortcutStatus.BrokenTarget => "대상 없음",
        ShortcutStatus.BrokenIcon   => "아이콘 없음",
        ShortcutStatus.Missing      => "파일 없음",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }
    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
