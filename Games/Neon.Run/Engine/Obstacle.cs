namespace NeonRun.Engine;

public enum ObstacleType { Wall, LowBar, HighBar, Crystal }

public sealed class Obstacle
{
    public double Z { get; set; }       // 터널 내 Z 위치 (앞쪽 = 큰 값)
    public int Lane { get; set; }       // -1=좌, 0=중앙, 1=우
    public ObstacleType Type { get; set; }
    public bool Collected { get; set; } // 크리스탈 수집 여부
    public bool Passed { get; set; }    // 지나감 여부

    public Obstacle(ObstacleType type, int lane, double z)
    {
        Type = type;
        Lane = lane;
        Z = z;
    }
}
