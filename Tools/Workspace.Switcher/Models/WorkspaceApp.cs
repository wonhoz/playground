namespace WorkspaceSwitcher.Models;

public class WorkspaceApp
{
    public string Name     { get; set; } = "";
    public string Path     { get; set; } = "";  // exe 경로 또는 URL
    public string Args     { get; set; } = "";  // 실행 인수 (선택)
    public bool   IsUrl    { get; set; }        // true면 URL로 실행
}
