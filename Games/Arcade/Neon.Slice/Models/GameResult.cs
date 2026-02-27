namespace NeonSlice.Models;

public sealed class GameResult
{
    public GameMode Mode { get; init; }
    public int Score { get; init; }
    public int MaxCombo { get; init; }
    public int Sliced { get; init; }
    public int Missed { get; init; }
    public DateTime PlayedAt { get; init; } = DateTime.Now;
}

public sealed class HighScoreData
{
    public int ClassicBest { get; set; }
    public int TimeAttackBest { get; set; }
    public int ZenBest { get; set; }
}
