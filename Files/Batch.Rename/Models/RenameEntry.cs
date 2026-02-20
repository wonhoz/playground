namespace BatchRename.Models;

/// <summary>이름 변경 대상 파일 1개의 상태</summary>
public class RenameEntry
{
    public string OriginalPath { get; init; } = "";
    public string OriginalName { get; init; } = "";   // 확장자 포함 파일명
    public string PreviewName  { get; set;  } = "";   // 변경될 이름 (실시간 갱신)
    public string ErrorMessage { get; set;  } = "";   // 패턴 오류 또는 중복
    public bool   HasError     => !string.IsNullOrEmpty(ErrorMessage);
}
