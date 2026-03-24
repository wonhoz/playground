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

    // 모드별 Top 3 (내림차순, 최대 3개)
    public List<int> ClassicTop3 { get; set; } = [];
    public List<int> TimeAttackTop3 { get; set; } = [];
    public List<int> ZenTop3 { get; set; } = [];
}
