using System.ComponentModel;

namespace FileDuplicates.Models;

public class FileEntry : INotifyPropertyChanged
{
    public string   Path         { get; init; } = "";
    public string   FileName     => System.IO.Path.GetFileName(Path);
    public long     Size         { get; init; }
    public string   SizeText     => FormatSize(Size);
    public DateTime LastModified { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024L              => $"{bytes} B",
        < 1024L * 1024       => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _                    => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}
