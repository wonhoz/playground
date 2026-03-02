namespace FileUnlocker.Models;

public class LockingProcess : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string AppType { get; set; } = "";
    public bool IsRestartable { get; set; }
    public ImageSource? Icon { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public string DirectoryPath => string.IsNullOrEmpty(ExecutablePath)
        ? ""
        : Path.GetDirectoryName(ExecutablePath) ?? "";

    public string ShortPath
    {
        get
        {
            if (string.IsNullOrEmpty(ExecutablePath)) return "(경로 없음)";
            if (ExecutablePath.Length <= 60) return ExecutablePath;
            return "..." + ExecutablePath[^57..];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
