namespace MouseFlick.Models;

public sealed class GestureProfile
{
    public string       Id           { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string       Name         { get; set; } = "";
    public bool         IsDefault    { get; set; } = false;
    public List<string> ProcessNames { get; set; } = [];  // 소문자 프로세스 이름
    public List<GestureAction> Actions { get; set; } = [];
}
