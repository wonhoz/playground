namespace SysClean.Models;

public class CleanTarget : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private long _size = -1;

    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string[] Paths { get; init; } = [];
    public bool IsGroup { get; init; }
    public string CleanerId { get; init; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public long Size
    {
        get => _size;
        set { _size = value; OnPropertyChanged(nameof(Size)); OnPropertyChanged(nameof(SizeText)); }
    }

    public string SizeText => _size < 0 ? "—" : FormatSize(_size);

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
