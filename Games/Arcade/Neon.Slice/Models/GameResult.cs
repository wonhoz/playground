namespace NeonSlice.Models;

public sealed class GameResult
{
    public GameMode   Mode       { get; init; }
    public Difficulty Difficulty { get; init; }
    public int Score { get; init; }
    public int MaxCombo { get; init; }
    public int Sliced { get; init; }
    public int Missed { get; init; }
    public DateTime PlayedAt { get; init; } = DateTime.Now;
}

public sealed class HighScoreData
{
    // ── 레거시 (v1.0) — Load 시 Top3ByKey로 마이그레이션 후 비어 있음 ────
    public int ClassicBest { get; set; }
    public int TimeAttackBest { get; set; }
    public int ZenBest { get; set; }
    public List<int>    ClassicTop3 { get; set; }         = [];
    public List<int>    TimeAttackTop3 { get; set; }      = [];
    public List<int>    ZenTop3 { get; set; }             = [];
    public List<string> ClassicTop3Dates { get; set; }    = [];
    public List<string> TimeAttackTop3Dates { get; set; } = [];
    public List<string> ZenTop3Dates { get; set; }        = [];

    // ── v1.1 — 모드×난이도별 Top3 (key: "Mode_Difficulty") ──────────────
    public Dictionary<string, List<int>>    Top3ByKey      { get; set; } = new();
    public Dictionary<string, List<string>> Top3DatesByKey { get; set; } = new();

    // 앱 설정 영속성
    public string LastMode       { get; set; } = "Classic";
    public string LastDifficulty { get; set; } = "Normal";
    public double WindowWidth    { get; set; } = 900;
    public double WindowHeight   { get; set; } = 760;

    // 사운드 설정
    public double BgmVolume { get; set; } = 0.6;
    public double SfxVolume { get; set; } = 0.8;
    public bool   Muted     { get; set; } = false;
}
