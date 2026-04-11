namespace SysClean.Models;

public class LargeFileEntry
{
    public string FullPath { get; init; } = "";
    public string FileName => Path.GetFileName(FullPath);
    public string Directory => Path.GetDirectoryName(FullPath) ?? "";
    public long SizeBytes { get; init; }
    public string SizeText => CleanTarget.FormatSize(SizeBytes);
    public string Extension => Path.GetExtension(FullPath).ToLower();
    public DateTime LastModified { get; init; }
    public string LastModifiedText => LastModified.ToString("yyyy-MM-dd HH:mm");
}
