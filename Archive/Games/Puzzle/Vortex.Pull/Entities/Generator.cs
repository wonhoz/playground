namespace VortexPull.Entities;

public enum GeneratorKind { Attract, Repel, Vortex }

/// <summary>배치된 발생기 — 위치·종류·강도.</summary>
public class Generator
{
    public double X, Y;
    public GeneratorKind Kind;
    public double Strength;  // 종류별 기본값 사용
}
