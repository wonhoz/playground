namespace DominoChain.Entities;

public enum TargetKind { Candle, Button, Ball }

/// <summary>
/// 마지막 도미노가 쓰러뜨려야 할 목표 오브젝트.
/// </summary>
public class Target
{
    public double    X      { get; init; }
    public double    Y      { get; init; }  // 하단 Y
    public double    W      { get; init; } = 20;
    public double    H      { get; init; } = 30;
    public TargetKind Kind  { get; init; } = TargetKind.Candle;

    public bool IsHit { get; set; } = false;

    /// <summary>마지막 도미노의 상단 모서리가 목표에 닿았는지 확인.</summary>
    public bool CheckCollision(double topX, double topY)
    {
        if (IsHit) return false;
        return topX >= X - W * 0.5 && topX <= X + W * 0.5 &&
               topY >= Y - H       && topY <= Y;
    }

    public void Reset() => IsHit = false;
}
