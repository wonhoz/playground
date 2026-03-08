namespace LeafGrow.Models;

// ── L-시스템 정의 ───────────────────────────────────────────────────

/// <summary>L-시스템 생성 규칙 1개</summary>
public record LRule(char From, string To);

/// <summary>식물 종 정의</summary>
public class PlantSpecies
{
    public string Name     { get; init; } = "";
    public string KorName  { get; init; } = "";
    public string Axiom    { get; init; } = "F";
    public List<LRule> Rules { get; init; } = [];
    public double Angle    { get; init; } = 25;    // 회전 각도 (도)
    public double Length   { get; init; } = 5;     // 기본 선분 길이
    public double LenDecay { get; init; } = 0.75;  // 깊이별 길이 감쇠율
    public Color  TrunkColor { get; init; } = Color.FromRgb(0x6B, 0x43, 0x26);
    public Color  LeafColor  { get; init; } = Color.FromRgb(0x3A, 0xC9, 0x5A);
    public Color  FlowerColor { get; init; } = Color.FromRgb(0xFF, 0xA0, 0xD0);
    public bool   HasFlower  { get; init; } = false;
    public int    MaxIter    { get; init; } = 5;   // 최대 전개 횟수
    public string Description { get; init; } = "";
}

// ── 렌더 세그먼트 ──────────────────────────────────────────────────

public enum SegType { Branch, Leaf, Flower }

public class Segment
{
    public double X1, Y1, X2, Y2;
    public double Thickness;
    public Color  Color;
    public SegType Type;
    public int    Depth;
}

// ── 성장 상태 ─────────────────────────────────────────────────────

public class GrowthState
{
    public PlantSpecies Species  { get; set; } = null!;
    public int          Iteration { get; set; } = 0;
    public string       LString  { get; set; } = "";   // 현재 전개 문자열
    public List<Segment> Segments { get; set; } = [];
    public double        Sun      { get; set; } = 0.5; // 0~1
    public double        Water    { get; set; } = 0.5;
    public double        Nutrients { get; set; } = 0.5;

    // 파생 수치
    public double GrowthRate => (Sun * 0.4 + Water * 0.35 + Nutrients * 0.25);
    public bool   IsFullyGrown => Iteration >= Species.MaxIter;
}

// ── 퍼즐 모드 ─────────────────────────────────────────────────────

public class PuzzleGoal
{
    public string Title       { get; init; } = "";
    public string Description { get; init; } = "";
    public int    TargetIter  { get; init; } = 3;
    public double MinSun      { get; init; } = 0.3;
    public double MaxSun      { get; init; } = 0.8;
    public double MinWater    { get; init; } = 0.3;
    public double MaxWater    { get; init; } = 0.8;
    public bool   NeedFlower  { get; init; } = false;
    public string SpeciesName { get; init; } = "";
}
