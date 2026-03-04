using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DiskLens.Models;

public class DiskItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private double _percentOfParent;
    private long _size;

    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long AllocatedSize { get; set; }
    public long FileCount { get; set; }
    public long FolderCount { get; set; }
    public DateTime LastModified { get; set; }
    public bool AccessDenied { get; set; }
    public int Depth { get; set; }
    public ObservableCollection<DiskItem> Children { get; } = [];

    public long Size
    {
        get => _size;
        set { _size = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeText)); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public double PercentOfParent
    {
        get => _percentOfParent;
        set { _percentOfParent = value; OnPropertyChanged(); OnPropertyChanged(nameof(PercentText)); }
    }

    // ── 표시용 ────────────────────────────────────────────────────────
    public string SizeText => FormatSize(Size);
    public string AllocatedText => FormatSize(AllocatedSize);
    public string FileCountText => FileCount.ToString("N0");
    public string FolderCountText => FolderCount.ToString("N0");
    public string LastModifiedText => LastModified == default ? "" : LastModified.ToString("yy-MM-dd HH:mm");
    public string PercentText => $"{PercentOfParent:F1}%";
    public string Icon => IsDirectory ? (AccessDenied ? "🔒" : "📁") : GetFileIcon(Name);

    // ── 크기 포맷 ─────────────────────────────────────────────────────
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1L << 40) return $"{bytes / (double)(1L << 40):F2} TB";
        if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):F2} GB";
        if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):F1} MB";
        if (bytes >= 1L << 10) return $"{bytes / (double)(1L << 10):F1} KB";
        return $"{bytes} B";
    }

    private static string GetFileIcon(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        string icon = ext switch
        {
            ".exe" or ".msi" or ".appx" => "\u2699",
            ".dll" or ".sys" or ".ocx" => "\uD83D\uDD27",
            ".zip" or ".7z" or ".rar" or ".gz" or ".tar" or ".bz2" => "\uD83D\uDDDC",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" or ".svg" or ".tif" => "\uD83D\uDDBC",
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => "\uD83C\uDFAC",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" => "\uD83C\uDFB5",
            ".pdf" => "\uD83D\uDCD5",
            ".doc" or ".docx" => "\uD83D\uDCDD",
            ".xls" or ".xlsx" or ".ppt" or ".pptx" => "\uD83D\uDCCA",
            ".txt" or ".log" or ".ini" or ".cfg" => "\uD83D\uDCC3",
            ".cs" or ".py" or ".js" or ".ts" or ".cpp" or ".c" or ".h" or ".java" or ".go" or ".rs" or ".rb" => "\uD83D\uDCBB",
            ".json" or ".xml" or ".yaml" or ".yml" or ".toml" => "\uD83D\uDCCB",
            ".html" or ".htm" or ".css" => "\uD83C\uDF10",
            ".db" or ".sqlite" or ".mdf" or ".sql" => "\uD83D\uDDC3",
            ".iso" or ".img" or ".vhd" or ".vmdk" => "\uD83D\uDCBF",
            _ => "\uD83D\uDCC4"
        };
        return icon;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ExtensionInfo
{
    public string Extension { get; set; } = "";
    public long TotalSize { get; set; }
    public long FileCount { get; set; }
    public string SizeText => DiskItem.FormatSize(TotalSize);
    public string AverageSizeText => FileCount > 0 ? DiskItem.FormatSize(TotalSize / FileCount) : "-";
}
