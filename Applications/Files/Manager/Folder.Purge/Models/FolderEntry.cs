using System.ComponentModel;

namespace FolderPurge.Models;

public enum FolderKind
{
    Empty,          // 완전히 비어있는 폴더
    VsArtifact,     // bin/obj만 남은 VS 빌드 아티팩트 폴더
    EmptyFile,      // 0바이트 파일
}

public class FolderEntry : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Path { get; init; } = string.Empty;
    public FolderKind Kind { get; init; }
    public long SizeBytes { get; init; }
    public int ItemCount { get; init; }   // 삭제될 파일/폴더 수

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public string KindBadge => Kind switch
    {
        FolderKind.Empty      => "빈 폴더",
        FolderKind.VsArtifact => "VS 아티팩트",
        FolderKind.EmptyFile  => "빈 파일",
        _                     => string.Empty
    };

    public string SizeText => SizeBytes switch
    {
        0           => "0 B",
        < 1024      => $"{SizeBytes} B",
        < 1048576   => $"{SizeBytes / 1024.0:F1} KB",
        < 1073741824 => $"{SizeBytes / 1048576.0:F1} MB",
        _           => $"{SizeBytes / 1073741824.0:F2} GB"
    };

    public string ItemCountText => Kind == FolderKind.Empty
        ? "비어있음"
        : $"{ItemCount:N0}개 항목";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
