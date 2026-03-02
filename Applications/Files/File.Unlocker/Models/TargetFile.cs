namespace FileUnlocker.Models;

public class TargetFile : INotifyPropertyChanged
{
    private int _lockCount;

    public string Path { get; set; } = "";

    public string DisplayName
    {
        get
        {
            var name = System.IO.Path.GetFileName(Path);
            return string.IsNullOrEmpty(name) ? Path : name;
        }
    }

    public bool IsFolder => Directory.Exists(Path);

    public int LockCount
    {
        get => _lockCount;
        set { _lockCount = value; OnPropertyChanged(nameof(LockCount)); OnPropertyChanged(nameof(LockCountText)); OnPropertyChanged(nameof(IsLocked)); }
    }

    public string LockCountText => _lockCount > 0 ? $"{_lockCount}개" : "잠금 없음";

    public bool IsLocked => _lockCount > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
