namespace ChordStrike.Models;

// ── 레인 정의 (8레인, 좌4 + 우4) ─────────────────────────────────
public static class Lanes
{
    public static readonly Key[] KeyBindings =
    [
        Key.A, Key.S, Key.D, Key.F,   // 레인 0~3 (왼손)
        Key.J, Key.K, Key.L, Key.OemSemicolon  // 레인 4~7 (오른손)
    ];

    public static readonly string[] Labels = ["A", "S", "D", "F", "J", "K", "L", ";"];
    public const int Count = 8;
}

// ── 음표 유형 ─────────────────────────────────────────────────────
public enum NoteType { Single, Hold }

// ── 판정 ──────────────────────────────────────────────────────────
public enum Judgment { Perfect, Good, Miss }

// ── 개별 음표 ─────────────────────────────────────────────────────
public class Note
{
    public int      Lane      { get; init; }
    public double   Beat      { get; init; }    // 박자 위치 (float beat index)
    public NoteType Type      { get; init; }
    public double   Duration  { get; init; }    // Hold 길이 (beats)

    // 런타임 상태
    public double   Y         { get; set; }     // 현재 화면 Y
    public bool     Judged    { get; set; }
    public Judgment LastJudge { get; set; }
}

// ── 채보 ──────────────────────────────────────────────────────────
public class Chart
{
    public string   Title   { get; init; } = "";
    public string   Artist  { get; init; } = "";
    public double   BPM     { get; init; } = 120;
    public List<Note> Notes { get; init; } = [];
}

// ── 결과 ──────────────────────────────────────────────────────────
public class ScoreResult
{
    public int Perfect { get; set; }
    public int Good    { get; set; }
    public int Miss    { get; set; }
    public int Combo   { get; set; }
    public int MaxCombo { get; set; }

    public int Total     => Perfect + Good + Miss;
    public double Acc    => Total == 0 ? 0 : (Perfect * 100.0 + Good * 50.0) / (Total * 100.0) * 100;
    public string Grade  => Acc >= 95 ? "S" : Acc >= 85 ? "A" : Acc >= 70 ? "B" : Acc >= 50 ? "C" : "D";

    public int TotalScore => Perfect * 300 + Good * 150 - Miss * 50;
}
