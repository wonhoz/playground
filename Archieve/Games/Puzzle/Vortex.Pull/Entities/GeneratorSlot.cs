namespace VortexPull.Entities;

/// <summary>
/// 플레이어가 발생기를 배치하는 슬롯.
/// 클릭 시 None → Attract → Repel → Vortex → None 순환.
/// </summary>
public class GeneratorSlot
{
    public double X, Y;
    public GeneratorKind? Kind = null;  // null = 비어있음

    public void Cycle()
    {
        Kind = Kind switch
        {
            null              => GeneratorKind.Attract,
            GeneratorKind.Attract => GeneratorKind.Repel,
            GeneratorKind.Repel   => GeneratorKind.Vortex,
            GeneratorKind.Vortex  => null,
            _                     => null
        };
    }

    /// <summary>슬롯에 배치된 발생기 반환 (비어있으면 null).</summary>
    public Generator? ToGenerator() => Kind is null ? null : new Generator
    {
        X = X, Y = Y, Kind = Kind.Value,
        Strength = Kind.Value switch
        {
            GeneratorKind.Attract => 22000,
            GeneratorKind.Repel   => 18000,
            GeneratorKind.Vortex  => 70,
            _                     => 0
        }
    };
}
