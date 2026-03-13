namespace PerspShift.Models;

public class Level
{
    public int     Number      { get; init; }
    public string  Title       { get; init; } = "";
    public bool[,] FrontTarget { get; init; } = new bool[5, 5]; // [x, y]
    public bool[,] TopTarget   { get; init; } = new bool[5, 5]; // [x, z]
    public bool[,] SideTarget  { get; init; } = new bool[5, 5]; // [y, z]
}
