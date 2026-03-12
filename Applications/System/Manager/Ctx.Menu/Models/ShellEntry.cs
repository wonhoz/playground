using System.Windows.Media;

namespace CtxMenu.Models;

public enum TargetType
{
    AllFiles,    // HKCR\*\shell
    Folder,      // HKCR\Directory\shell
    Background,  // HKCR\Directory\Background\shell
    Drive,       // HKCR\Drive\shell
    Extension,   // HKCR\.ext\shell (확장자별)
}

public enum RegistryScope
{
    System,  // HKLM
    User,    // HKCU
}

public class ShellEntry : INotifyPropertyChanged
{
    private bool _isEnabled;

    public string KeyName        { get; set; } = "";
    public string DisplayName    { get; set; } = "";
    public string Command        { get; set; } = "";
    public string IconPath       { get; set; } = "";
    public TargetType TargetType { get; set; }
    public RegistryScope Scope   { get; set; }
    public string RegistryPath   { get; set; } = "";
    public string ExtFilter      { get; set; } = ""; // Extension 타입일 때 확장자

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }
    }

    public string TargetDisplay => TargetType switch
    {
        TargetType.AllFiles   => "모든 파일",
        TargetType.Folder     => "폴더",
        TargetType.Background => "배경",
        TargetType.Drive      => "드라이브",
        TargetType.Extension  => ExtFilter,
        _                     => ""
    };

    public string ScopeDisplay => Scope == RegistryScope.System ? "시스템" : "사용자";

    public string DisplayOrKey => string.IsNullOrWhiteSpace(DisplayName) ? KeyName : DisplayName;

    // ── 뱃지 색상 ────────────────────────────────────────────────
    public Brush TargetBg => TargetType switch
    {
        TargetType.AllFiles   => new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A)),
        TargetType.Folder     => new SolidColorBrush(Color.FromRgb(0x1A, 0x28, 0x1A)),
        TargetType.Background => new SolidColorBrush(Color.FromRgb(0x2A, 0x20, 0x10)),
        TargetType.Drive      => new SolidColorBrush(Color.FromRgb(0x28, 0x1A, 0x2A)),
        TargetType.Extension  => new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x28)),
        _                     => Brushes.Transparent,
    };

    public Brush TargetFg => TargetType switch
    {
        TargetType.AllFiles   => new SolidColorBrush(Color.FromRgb(0x60, 0xB0, 0xFF)),
        TargetType.Folder     => new SolidColorBrush(Color.FromRgb(0x60, 0xD0, 0x80)),
        TargetType.Background => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x40)),
        TargetType.Drive      => new SolidColorBrush(Color.FromRgb(0xC0, 0x80, 0xFF)),
        TargetType.Extension  => new SolidColorBrush(Color.FromRgb(0xA0, 0xB0, 0xD0)),
        _                     => Brushes.Gray,
    };

    public Brush ScopeFg => Scope == RegistryScope.System
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x40))
        : new SolidColorBrush(Color.FromRgb(0x70, 0xB0, 0xFF));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
