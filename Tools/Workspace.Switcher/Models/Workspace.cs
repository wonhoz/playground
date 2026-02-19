namespace WorkspaceSwitcher.Models;

public class Workspace
{
    public Guid   Id      { get; set; } = Guid.NewGuid();
    public string Name    { get; set; } = "새 워크스페이스";
    public string Emoji   { get; set; } = "💼";
    public string Color   { get; set; } = "#2C3E50";
    public List<WorkspaceApp> Apps { get; set; } = [];
}
