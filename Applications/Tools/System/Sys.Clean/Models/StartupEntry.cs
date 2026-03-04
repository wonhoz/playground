namespace SysClean.Models;

public enum StartupLocation
{
    HklmRun,
    HkcuRun,
}

public class StartupEntry : INotifyPropertyChanged
{
    private bool _isEnabled;

    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public StartupLocation Location { get; init; }

    public string LocationText => Location == StartupLocation.HklmRun
        ? "HKLM (모든 사용자)"
        : "HKCU (현재 사용자)";

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => _isEnabled ? "활성화" : "비활성화";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
