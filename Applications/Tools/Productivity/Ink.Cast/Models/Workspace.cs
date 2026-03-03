namespace InkCast.Models;

/// <summary>워크스페이스 모델</summary>
public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastOpenedAt { get; set; } = DateTime.Now;

    public override string ToString() => Name;
}
