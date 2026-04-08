using System.IO;

namespace MarkView.Models;

public class MarkDocument
{
    public Guid Id { get; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsModified { get; set; }
    public double ScrollY { get; set; } = 0;
    public bool IsNew => string.IsNullOrEmpty(FilePath);

    public string? CustomTitle { get; set; }

    public string TabTitle
    {
        get
        {
            var name = CustomTitle ?? (IsNew ? "새 문서" : Path.GetFileName(FilePath));
            return IsModified ? name + " •" : name;
        }
    }

    public string FileName => IsNew ? "새 문서" : Path.GetFileName(FilePath);
    public string? Directory => IsNew ? null : Path.GetDirectoryName(FilePath);
}
