namespace DriveBench.Models;

public class SmartAttribute
{
    public int    Id        { get; init; }
    public string Name      { get; init; } = "";
    public int    Current   { get; init; }
    public int    Worst     { get; init; }
    public int    Threshold { get; init; }
    public string Raw       { get; init; } = "";
    public bool   IsWarning => Threshold > 0 && Current <= Threshold;
    public string StatusColor => IsWarning ? "#EF4444" : "#22C55E";
    public string StatusText  => IsWarning ? "⚠ 경고" : "정상";
}
