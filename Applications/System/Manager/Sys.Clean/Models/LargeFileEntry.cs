namespace SysClean.Models;

public class LargeFileEntry
{
    public string FullPath { get; init; } = "";
    public string FileName => Path.GetFileName(FullPath);
    public string Directory => Path.GetDirectoryName(FullPath) ?? "";
    public long SizeBytes { get; init; }
    public string SizeText => CleanTarget.FormatSize(SizeBytes);
    public string Extension => Path.GetExtension(FullPath).ToLower();
    public string ExtensionColor => Extension switch
    {
        ".iso" or ".img" or ".vmdk" or ".vhd" or ".vhdx" => "#F06292",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" => "#FFB74D",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".ts" or ".m2ts" => "#4FC3F7",
        ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" or ".aac" => "#81C784",
        ".pst" or ".ost" or ".mdb" or ".accdb" or ".db" or ".sqlite" => "#E57373",
        ".exe" or ".msi" or ".dll" => "#FF8A65",
        ".pdf" or ".docx" or ".xlsx" or ".pptx" or ".doc" or ".xls" => "#9575CD",
        ".bak" or ".log" or ".tmp" or ".dmp" => "#90A4AE",
        _ => "#7986CB"
    };
    public DateTime LastModified { get; init; }
    public string LastModifiedText => LastModified.ToString("yyyy-MM-dd HH:mm");
}
