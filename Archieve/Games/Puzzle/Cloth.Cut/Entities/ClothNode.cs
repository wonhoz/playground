namespace ClothCut.Entities;

/// <summary>
/// Verlet 통합 기반 질점(Mass Point).
/// OldX/OldY로 이전 위치를 기억해 속도를 암묵적으로 표현.
/// </summary>
public class ClothNode
{
    public double X,    Y;       // 현재 위치
    public double OldX, OldY;   // 이전 위치 (Verlet용)
    public bool   IsPinned;     // true = 고정 핀 (중력 무시)
    public int    Row, Col;     // 격자 좌표

    /// <summary>이전 위치를 현재와 동일하게 초기화 (정지 상태 시작).</summary>
    public void InitOld() { OldX = X; OldY = Y; }

    public void ResetTo(double x, double y)
    {
        X = OldX = x;
        Y = OldY = y;
    }
}
