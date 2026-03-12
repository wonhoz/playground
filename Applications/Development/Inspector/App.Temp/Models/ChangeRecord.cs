namespace AppTemp.Models;

public enum ChangeType { Created, Modified, Deleted, Renamed }
public enum ChangeCategory { File, Registry }

public class ChangeRecord
{
    public DateTime    Timestamp  { get; init; } = DateTime.Now;
    public ChangeType  Type       { get; init; }
    public ChangeCategory Category { get; init; }

    /// <summary>파일 전체 경로 또는 레지스트리 키 경로</summary>
    public string Path     { get; init; } = "";
    public string? OldPath { get; init; }  // Renamed 시 이전 경로

    /// <summary>레지스트리 변경 시 값 이름 / 이전 값</summary>
    public string? ValueName { get; init; }
    public string? OldValue  { get; init; }
    public string? NewValue  { get; init; }

    public string TypeLabel => Type switch
    {
        ChangeType.Created  => "생성",
        ChangeType.Modified => "수정",
        ChangeType.Deleted  => "삭제",
        ChangeType.Renamed  => "이름변경",
        _ => "?"
    };

    public string CategoryLabel => Category == ChangeCategory.File ? "파일" : "레지스트리";

    public string DisplayPath => OldPath != null
        ? $"{OldPath}  →  {Path}"
        : Path;

    public string TimestampStr => Timestamp.ToString("HH:mm:ss");
}

public class RegistrySnapshot
{
    /// <summary>key = 레지스트리 경로, value = (valueName → valueData) 딕셔너리</summary>
    public Dictionary<string, Dictionary<string, string>> Keys { get; } = [];
}
