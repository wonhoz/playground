namespace DashCity.Engine;

public enum ObjectKind
{
    // 장애물
    Barrier,        // 낮은 바리케이드 (점프로 회피)
    Train,          // 전동차 장애물 (옆으로 회피)
    Beam,           // 높은 빔 (슬라이드로 회피)

    // 수집
    Coin,           // 코인
    Magnet,         // 자석 파워업
    Shield,         // 보호막 파워업
    Multiplier,     // 2x 스코어
    Jetpack,        // 제트팩 (일시 무적+비행)
}

public sealed class WorldObject
{
    public ObjectKind Kind { get; }
    public int Lane { get; }          // -1, 0, 1
    public double Z { get; set; }     // 월드 Z
    public double Y { get; set; }     // 높이 (코인 패턴용)
    public bool Active { get; set; } = true;

    public WorldObject(ObjectKind kind, int lane, double z, double y = 0)
    {
        Kind = kind;
        Lane = lane;
        Z = z;
        Y = y;
    }

    public bool IsObstacle => Kind is ObjectKind.Barrier or ObjectKind.Train or ObjectKind.Beam;
    public bool IsPowerUp => Kind is ObjectKind.Magnet or ObjectKind.Shield or ObjectKind.Multiplier or ObjectKind.Jetpack;
}
